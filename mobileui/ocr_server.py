"""
ocr_server.py: Nhận diện chỉ số đồng hồ điện bằng MiniCPM-V qua Ollama.

Cách làm để giảm sai số (so với việc gửi cả ảnh một lần):
  1. Tự tìm ô số màu đỏ (chữ số thập phân) -> nắn thẳng ảnh -> cắt đúng dải chữ số -> phóng to.
  2. Mỗi phiên bản ảnh được hỏi AI RIÊNG (một ảnh mỗi lần), kết quả được bỏ phiếu.
  3. Ép định dạng trả lời bằng JSON schema; prompt KHÔNG chứa số ví dụ (AI hay chép lại ví dụ).
  4. Độ tin cậy do mức độ khớp giữa các lần đọc quyết định, không tin vào "tự đánh giá" của AI.
  5. Nếu biết chỉ số kỳ trước, dùng để loại các kết quả vô lý.
"""
import base64
import io
import json
import math
import os
import re
import sys
import threading
import time
from collections import Counter
from decimal import Decimal, InvalidOperation
from typing import Any, Dict, List, Optional, Tuple

import requests
from flask import Flask, jsonify, request
from PIL import Image, ImageOps
from werkzeug.exceptions import RequestEntityTooLarge

try:  # OpenCV giúp tự cắt vùng số; không có thì vẫn chạy (đọc cả ảnh)
    import cv2
    import numpy as np
except ImportError:  # pragma: no cover
    cv2 = None
    np = None

if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except AttributeError:
        pass

# ----- Cấu hình qua biến môi trường; không cần file config.py -----
OLLAMA_BASE_URL = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434").rstrip("/")
VLM_MODEL = os.getenv("OLLAMA_MODEL", "minicpm-v:latest")
OLLAMA_KEEP_ALIVE = os.getenv("OLLAMA_KEEP_ALIVE", "30m")
MAX_KWH_JUMP = float(os.getenv("OCR_MAX_KWH_JUMP", "3000"))
# The mobile client gives up after 35 seconds. Keep server work bounded below
# that limit, even if an environment override is accidentally set too high.
TOTAL_BUDGET_S = min(max(float(os.getenv("OCR_TOTAL_BUDGET_S", "30")), 1.0), 32.0)
READ_TIMEOUT_S = min(max(float(os.getenv("OCR_READ_TIMEOUT_S", "28")), 1.0), 32.0)
OCR_MAX_CONCURRENT = max(1, int(os.getenv("OCR_MAX_CONCURRENT", "1")))
DEBUG_DIR = os.getenv("OCR_DEBUG_DIR") or None
_OCR_SEMAPHORE = threading.BoundedSemaphore(OCR_MAX_CONCURRENT)

app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = 10 * 1024 * 1024

# Chỉ ép KIỂU dữ liệu, không đưa giá trị ví dụ nào vào prompt.
ANSWER_SCHEMA = {
    "type": "object",
    "properties": {
        "meter_type": {"type": "string", "enum": ["mechanical", "electronic"]},
        "integer_digits": {"type": "string"},
        "decimal_digit": {"type": "string"},
    },
    "required": ["meter_type", "integer_digits", "decimal_digit"],
}

PROMPT_OCR_METER = (
    "You are reading the main kWh register of an electricity meter from a photo.\n"
    "Step 1: decide the meter type: \"mechanical\" (rotating digit wheels) or "
    "\"electronic\" (LCD with segment digits).\n"
    "Step 2: read ONLY the main energy register, digits from left to right. Ignore serial numbers, "
    "printed labels, the unit 'kWh', voltage/current/tariff indicators and anything outside the register.\n"
    "- Mechanical: wheels with a black background are the integer digits; a wheel with a red "
    "background (usually the last one) is the tenths digit.\n"
    "- Electronic: digits before the decimal point are the integer digits; the digit after the "
    "decimal point is the tenths digit.\n"
    "- If there is no tenths digit, use \"0\".\n"
    "- If a wheel is between two digits, report the digit that is most visible.\n"
    "- Never guess. If the digits are not clearly readable, return an empty string for integer_digits.\n"
    "Reply with JSON using exactly the fields meter_type, integer_digits and decimal_digit. "
    "integer_digits and decimal_digit are strings containing only digits "
    "(keep leading zeros, no spaces, no dot)."
)

LOAI_CONG_TO = {"mechanical": "Công tơ cơ", "electronic": "Công tơ điện tử"}

# Dấu của góc xoay khi nắn thẳng ảnh (đã kiểm thử với ảnh nghiêng giả lập).
ROTATE_SIGN = 1


# =====================================================================
# Xử lý ảnh
# =====================================================================
def encode_image_to_base64(image: Image.Image) -> str:
    """Chuyển ảnh Pillow sang chuỗi Base64 (JPEG chất lượng cao)."""
    buffer = io.BytesIO()
    image.save(buffer, format="JPEG", quality=95)
    return base64.b64encode(buffer.getvalue()).decode("utf-8")


def _fit_within(image: Image.Image, size: Tuple[int, int]) -> Image.Image:
    """Giới hạn kích thước ảnh mà không phóng đại ảnh nguồn nhỏ."""
    if image.width <= size[0] and image.height <= size[1]:
        return image
    return ImageOps.contain(image, size, method=Image.Resampling.LANCZOS)


def _load_image(image_bytes: bytes) -> Image.Image:
    with Image.open(io.BytesIO(image_bytes)) as source:
        if source.width * source.height > 25_000_000:
            raise ValueError("Ảnh có độ phân giải quá lớn. Hãy chọn ảnh khác.")
        image = ImageOps.exif_transpose(source).convert("RGB")
    # Giữ độ phân giải 2000px để khi cắt vùng số vẫn còn đủ chi tiết.
    return _fit_within(image, (2000, 2000))


def _rotate(image: Image.Image, tilt: float, center: Tuple[float, float]) -> Image.Image:
    if abs(tilt) < 0.5:
        return image
    return image.rotate(
        ROTATE_SIGN * tilt,
        resample=Image.Resampling.BICUBIC,
        center=center,
        fillcolor=(0, 0, 0),
    )


def _enhance(image: Image.Image) -> Image.Image:
    """Tăng tương phản cục bộ nhưng GIỮ MÀU (không chuyển đen trắng vì sẽ mất ô đỏ)."""
    if cv2 is None:
        return ImageOps.autocontrast(image, cutoff=1)
    lab = cv2.cvtColor(np.array(image), cv2.COLOR_RGB2LAB)
    l_ch, a_ch, b_ch = cv2.split(lab)
    l_ch = cv2.createCLAHE(clipLimit=2.5, tileGridSize=(4, 4)).apply(l_ch)
    return Image.fromarray(cv2.cvtColor(cv2.merge((l_ch, a_ch, b_ch)), cv2.COLOR_LAB2RGB))


def _upscale(image: Image.Image, target_width: int = 1280, max_factor: float = 4) -> Image.Image:
    """Phóng to vùng đã cắt để chữ số đủ lớn; thu nhỏ nếu ảnh quá lớn."""
    if image.width < target_width:
        factor = min(target_width / image.width, max_factor)
        return image.resize(
            (round(image.width * factor), round(image.height * factor)),
            Image.Resampling.LANCZOS,
        )
    return ImageOps.contain(image, (1600, 1600), method=Image.Resampling.LANCZOS)


def _find_red_box(image: Image.Image) -> Optional[Dict[str, Any]]:
    """
    Tìm ô số màu đỏ (chữ số thập phân của công tơ cơ) làm mốc và đo độ nghiêng.
    Chỉ nhận ô đỏ có dải nền tối (các ô số đen) bên trái. Không thấy -> None (đọc cả ảnh).
    """
    if cv2 is None:
        return None

    hsv = cv2.cvtColor(np.array(image), cv2.COLOR_RGB2HSV)
    img_h, img_w = hsv.shape[:2]
    img_area = img_h * img_w

    # Thử ngưỡng chặt trước, nếu không thấy mới nới lỏng (ảnh thiếu sáng).
    for s_min, v_min in ((100, 70), (60, 40)):
        mask = cv2.inRange(hsv, (0, s_min, v_min), (10, 255, 255)) | cv2.inRange(
            hsv, (170, s_min, v_min), (180, 255, 255)
        )
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        candidates = []
        for c in contours:
            area = cv2.contourArea(c)
            if not (img_area * 0.0002 <= area <= img_area * 0.05):
                continue
            (cx, cy), (rw, rh), angle = cv2.minAreaRect(c)
            short, long_ = min(rw, rh), max(rw, rh)
            if short < 8:
                continue
            fill = area / (rw * rh)
            ratio = short / long_
            if fill < 0.6 or not 0.35 <= ratio <= 1.0:
                continue
            tilt = angle % 90.0
            if tilt > 45:
                tilt -= 90
            candidates.append((area * fill, cx, cy, short, long_, tilt, fill))

        candidates.sort(reverse=True)
        best, best_score = None, 0.0
        for _, cx, cy, bw, bh, tilt, fill in candidates[:4]:
            straight = _rotate(image, tilt, (cx, cy))
            val = cv2.cvtColor(np.array(straight), cv2.COLOR_RGB2HSV)[:, :, 2]
            x0, x1 = int(max(0, cx - 5.5 * bw)), int(max(0, cx - 0.6 * bw))
            y0, y1 = int(max(0, cy - 0.3 * bh)), int(min(img_h, cy + 0.3 * bh))
            region = val[y0:y1, x0:x1]
            if region.size == 0:
                continue
            dark_ratio = float((region < 90).mean())
            score = dark_ratio * fill
            if dark_ratio >= 0.4 and score > best_score:
                best_score = score
                best = {"center": (cx, cy), "width": bw, "height": bh, "tilt": tilt}
        if best:
            return best
    return None


def _crop_digit_strip(image, box, n_int, margin_v=0.45, margin_l=0.3) -> Image.Image:
    """Nắn thẳng ảnh rồi cắt dải các ô số, tính từ ô đỏ sang trái."""
    straight = _rotate(image, box["tilt"], box["center"])
    cx, cy = box["center"]
    bw, bh = box["width"], box["height"]

    left = cx - bw / 2 - (n_int + margin_l) * bw * 1.1
    right = cx + bw / 2 + 0.6 * bw
    top = cy - bh / 2 - margin_v * bh
    bottom = cy + bh / 2 + margin_v * bh
    return straight.crop((
        int(max(0, round(left))),
        int(max(0, round(top))),
        int(min(straight.width, round(right))),
        int(min(straight.height, round(bottom))),
    ))


def _build_variants(image, box, previous_reading) -> List[Tuple[str, Image.Image]]:
    """Các phiên bản ảnh theo thứ tự ưu tiên; mỗi phiên bản là một lần hỏi AI riêng."""
    variants: List[Tuple[str, Image.Image]] = []
    full = _fit_within(image, (1600, 1600))

    if box is not None:
        prev_digits = len(str(int(previous_reading))) if previous_reading else 0
        n_int = min(max(5, prev_digits), 7)
        strip = _crop_digit_strip(image, box, n_int)
        strip_wide = _crop_digit_strip(image, box, n_int, margin_v=0.9, margin_l=1.0)
        variants.append(("strip", _upscale(strip)))
        variants.append(("strip_enhanced", _upscale(_enhance(strip))))
        variants.append(("strip_wide", _upscale(strip_wide)))
        variants.append(("full", full))
    else:
        w, h = image.size
        center = image.crop((int(w * 0.15), int(h * 0.15), int(w * 0.85), int(h * 0.85)))
        variants.append(("full", full))
        variants.append(("full_enhanced", _enhance(full)))
        variants.append(("center", _fit_within(center, (1600, 1600))))
    return variants


def _save_debug(name: str, image: Image.Image) -> None:
    if not DEBUG_DIR:
        return
    os.makedirs(DEBUG_DIR, exist_ok=True)
    image.save(os.path.join(DEBUG_DIR, f"{int(time.time() * 1000)}_{name}.jpg"), quality=92)


# =====================================================================
# Gọi AI và phân tích kết quả
# =====================================================================
def _ask_model(image: Image.Image, timeout: float) -> Dict[str, Any]:
    """Một lần hỏi = một ảnh (model đọc nhiều ảnh cùng lúc thường kém hơn)."""
    payload = {
        "model": VLM_MODEL,
        "messages": [
            {
                "role": "user",
                "content": PROMPT_OCR_METER,
                "images": [encode_image_to_base64(image)],
            }
        ],
        "format": ANSWER_SCHEMA,
        "stream": False,
        "keep_alive": OLLAMA_KEEP_ALIVE,
        "options": {"temperature": 0, "seed": 42, "num_predict": 96},
    }
    response = requests.post(
        f"{OLLAMA_BASE_URL}/api/chat",
        json=payload,
        timeout=(min(3.0, timeout), timeout),
    )
    if response.status_code != 200:
        detail = response.text[:500]
        raise RuntimeError(f"Lỗi từ Ollama API ({response.status_code}): {detail}")

    result = response.json()
    message = result.get("message")
    if not isinstance(message, dict):
        raise ValueError("Ollama không trả về đối tượng message hợp lệ.")

    content = message.get("content", "")
    if isinstance(content, dict):
        return content

    text = str(content or "").strip()
    text = re.sub(
        r"^```(?:json)?\s*|\s*```$",
        "",
        text,
        flags=re.IGNORECASE,
    )
    decoder = json.JSONDecoder()
    for index, character in enumerate(text):
        if character != "{":
            continue
        try:
            value, _ = decoder.raw_decode(text[index:])
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            return value

    done_reason = result.get("done_reason")
    if done_reason == "length":
        raise RuntimeError(
            f"Ollama model {VLM_MODEL} dừng do hết token trước khi trả JSON. "
            "Hãy dùng model minicpm-v:latest."
        )
    raise ValueError(
        f"Ollama model {VLM_MODEL} không trả về JSON hợp lệ "
        f"(done_reason={done_reason!r})."
    )


def _to_str(value: Any) -> str:
    if value is None or isinstance(value, bool):
        return ""
    return re.sub(r"\s+", "", str(value))


def _parse_answer(data: Any, name: str) -> Optional[Tuple[int, str]]:
    """Trả về (chỉ số dạng 'phần mười', loại công tơ), ví dụ 12345.6 -> 123456. None nếu không đọc rõ."""
    if not isinstance(data, dict):
        return None

    digits = _to_str(data.get("integer_digits"))
    decimal = _to_str(data.get("decimal_digit"))
    meter_type = data.get("meter_type")
    if meter_type not in LOAI_CONG_TO:
        return None

    # Chấp nhận trường hợp AI nhét luôn phần thập phân vào integer_digits ("12345.6").
    m = re.fullmatch(r"(\d{3,8})[.,](\d)", digits)
    if m:
        digits = m.group(1)
        decimal = m.group(2)

    if not re.fullmatch(r"\d{3,8}", digits) or not re.fullmatch(r"\d", decimal):
        print(f"[ocr:{name}] không đọc rõ: integer={digits!r} decimal={decimal!r}", file=sys.stderr)
        return None
    return int(digits) * 10 + int(decimal), meter_type


def _plausible(tenths: int, previous: Optional[float]) -> bool:
    if previous is None:
        return True
    return previous - 0.05 <= tenths / 10.0 <= previous + MAX_KWH_JUMP


def _has_exact_pair(readings, previous) -> bool:
    counts = Counter(v for _, v, _ in readings if _plausible(v, previous))
    return any(c >= 2 for c in counts.values())


def _vote(readings, previous) -> Optional[Dict[str, Any]]:
    """
    Bỏ phiếu giữa các lần đọc. Cần ít nhất 2 lần khớp mới chấp nhận.
    Trả về dict {tenths, agree, total, exact, meter_type} hoặc None.
    """
    if not readings:
        return None

    pool = [r for r in readings if _plausible(r[1], previous)]
    if not pool:
        return None
    values = [v for _, v, _ in pool]

    def meter_type_of(selected):
        return Counter(t for _, v, t in pool if selected(v)).most_common(1)[0][0]

    # 1) Khớp hoàn toàn (cả chữ số thập phân).
    best, agree = Counter(values).most_common(1)[0]
    if agree >= 2:
        return {"tenths": best, "agree": agree, "total": len(pool), "exact": True,
                "meter_type": meter_type_of(lambda v: v == best)}

    # 2) Phần nguyên khớp, chữ số thập phân khác nhau (bánh xe đỏ hay nằm lửng giữa hai số).
    int_part, int_agree = Counter(v // 10 for v in values).most_common(1)[0]
    if int_agree >= 2:
        decimals = Counter(v % 10 for v in values if v // 10 == int_part)
        top = max(decimals.values())
        digit = min(d for d, c in decimals.items() if c == top)  # hòa phiếu: lấy số nhỏ hơn (bánh xe đã đi qua)
        return {"tenths": int_part * 10 + digit, "agree": int_agree, "total": len(pool), "exact": False,
                "meter_type": meter_type_of(lambda v: v // 10 == int_part)}
    return None


def _confidence(vote: Dict[str, Any], strip_found: bool) -> str:
    if not vote["exact"]:
        return "Trung bình"
    unanimous = vote["agree"] == vote["total"]
    if (vote["agree"] >= 3 or strip_found) and unanimous:
        return "Cao"
    return "Trung bình"


def read_meter_from_image(image_bytes: bytes, previous_reading: Optional[float] = None) -> Dict[str, Any]:
    """
    Đọc chỉ số công tơ điện từ ảnh.

    previous_reading: chỉ số kỳ trước (không bắt buộc). Có thì dùng để loại các kết quả vô lý.
    Giữ nguyên các khóa kết quả cũ: success, model_used, meter_type, full_reading, integer_part,
    decimal_part, total_kwh, confidence, note (thêm: needs_review, votes, attempts, strip_found).
    """
    model = VLM_MODEL
    try:
        image = _load_image(image_bytes)
        box = _find_red_box(image)

        readings: List[Tuple[str, int, str]] = []  # (tên phiên bản, giá trị phần mười, loại công tơ)
        connection_error: Optional[Exception] = None
        started = time.monotonic()
        deadline = started + TOTAL_BUDGET_S

        for name, variant in _build_variants(image, box, previous_reading):
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                break
            _save_debug(name, variant)
            try:
                data = _ask_model(variant, min(READ_TIMEOUT_S, remaining))
            except requests.exceptions.RequestException as error:
                connection_error = error
                break
            except (ValueError, json.JSONDecodeError) as error:
                print(f"[ocr:{name}] lỗi phân tích trả lời: {error}", file=sys.stderr)
                continue

            parsed = _parse_answer(data, name)
            if parsed is None:
                continue
            readings.append((name, parsed[0], parsed[1]))
            if _has_exact_pair(readings, previous_reading):
                break  # hai lần đọc độc lập khớp nhau -> dừng sớm cho nhanh

        if connection_error is not None and not readings:
            if isinstance(connection_error, requests.exceptions.Timeout):
                return {
                    "success": False,
                    "error": f"Ollama phản hồi quá lâu (>{READ_TIMEOUT_S:.0f}s). Model `{model}` có thể đang nạp "
                             "hoặc máy quá tải, hãy thử lại.",
                    "model_used": model,
                }
            return {
                "success": False,
                "error": f"Không thể kết nối tới Ollama tại {OLLAMA_BASE_URL}. Vui lòng đảm bảo Ollama "
                         f"đang chạy và đã kéo model `{model}` (`ollama run {model}`).",
                "model_used": model,
            }

        vote = _vote(readings, previous_reading)
        candidates = [round(v / 10.0, 1) for _, v, _ in readings]

        if vote is None:
            if not readings:
                error = ("AI không đọc rõ được chữ số nào trên ảnh. Hãy chụp gần màn hình công tơ, đủ sáng, "
                         "nhìn thẳng và tránh chói, hoặc nhập chỉ số thủ công.")
            elif not any(_plausible(value, previous_reading) for _, value, _ in readings):
                error = (
                    "Các chỉ số AI đọc được đều vượt quá mức tiêu thụ hợp lý so với kỳ trước. "
                    "Hãy kiểm tra lại ảnh và chỉ số cũ."
                )
            elif connection_error is not None:
                error = (
                    "Hết thời gian chờ trước khi có đủ lần đọc để xác nhận chỉ số. "
                    "Hãy thử lại khi Ollama ít tải hơn hoặc nhập chỉ số thủ công."
                )
            else:
                error = (f"Các lần đọc không khớp nhau ({', '.join(map(str, candidates))} kWh). "
                         "Hãy chụp gần hơn hoặc nhập chỉ số thủ công.")
            return {"success": False, "error": error, "candidates": candidates, "model_used": model}

        total = vote["tenths"] / 10.0
        confidence = _confidence(vote, box is not None)

        notes = [f"{vote['agree']}/{vote['total']} lần đọc khớp nhau"]
        notes.append("đã cắt đúng vùng số" if box is not None else "không tìm thấy ô đỏ nên đọc cả ảnh")
        if not vote["exact"]:
            notes.append("chữ số thập phân chưa chắc chắn (có thể số đỏ đang lửng giữa chừng)")
        if previous_reading is not None and total < previous_reading:
            notes.append(f"CẢNH BÁO: nhỏ hơn chỉ số kỳ trước ({previous_reading})")
            confidence = "Thấp"

        return {
            "success": True,
            "meter_type": LOAI_CONG_TO[vote["meter_type"]],
            "full_reading": f"{total:.1f}",
            "integer_part": vote["tenths"] // 10,
            "decimal_part": vote["tenths"] % 10,
            "total_kwh": total,
            "confidence": confidence,
            "note": "; ".join(notes),
            "needs_review": confidence != "Cao",
            "votes": {"agree": vote["agree"], "total": vote["total"]},
            "attempts": candidates,
            "strip_found": box is not None,
            "model_used": model,
        }

    except RuntimeError as e:
        return {"success": False, "error": str(e), "model_used": model}
    except Exception as e:
        return {"success": False, "error": f"Lỗi xử lý OCR: {str(e)}", "model_used": model}


@app.errorhandler(RequestEntityTooLarge)
def handle_file_too_large(_error):
    return jsonify({"error": "Ảnh vượt quá dung lượng tối đa 10 MB."}), 413


@app.get("/")
def health_check():
    return jsonify({
        "status": "ok",
        "service": "Python OCR Server",
        "model": VLM_MODEL,
    })


@app.post("/api/ocr-dong-ho")
def api_ocr_dong_ho():
    image_file = request.files.get("image")
    if image_file is None or not image_file.filename:
        return jsonify({"error": "Không tìm thấy file ảnh."}), 400

    try:
        ma_phong = int(request.form.get("maPhong", "0"))
        chi_so_cu = Decimal(request.form.get("chiSoCu", "-1"))
        thang = int(request.form.get("thang", "0"))
        nam = int(request.form.get("nam", "0"))
        if (
            ma_phong <= 0
            or not chi_so_cu.is_finite()
            or not math.isfinite(float(chi_so_cu))
            or chi_so_cu < 0
            or not 1 <= thang <= 12
            or nam < 1
        ):
            raise ValueError
    except (InvalidOperation, TypeError, ValueError):
        return jsonify({
            "error": "Mã phòng, kỳ hóa đơn hoặc chỉ số cũ không hợp lệ."
        }), 400

    if not _OCR_SEMAPHORE.acquire(blocking=False):
        return jsonify({
            "error": "Máy chủ OCR đang xử lý ảnh khác. Vui lòng đợi một chút rồi thử lại."
        }), 503

    try:
        result = read_meter_from_image(image_file.read(), float(chi_so_cu))
    finally:
        _OCR_SEMAPHORE.release()

    if not result.get("success"):
        error = str(result.get("error", "Không thể đọc chỉ số từ ảnh."))
        status = 502 if "Ollama" in error else 422
        app.logger.warning("OCR thất bại cho phòng %s: %s", ma_phong, error)
        return jsonify({"error": error, "details": result}), status

    try:
        chi_so_moi = Decimal(str(result["total_kwh"]))
    except (KeyError, InvalidOperation, TypeError, ValueError):
        app.logger.exception("OCR trả kết quả thiếu hoặc sai định dạng.")
        return jsonify({"error": "Kết quả OCR không hợp lệ."}), 502

    if not chi_so_moi.is_finite():
        return jsonify({"error": "Kết quả OCR không phải chỉ số hợp lệ."}), 502

    if chi_so_moi < chi_so_cu:
        return jsonify({
            "error": (
                f"Chỉ số OCR ({chi_so_moi} kWh) nhỏ hơn chỉ số cũ "
                f"({chi_so_cu} kWh). Hãy kiểm tra ảnh và chỉ số."
            ),
            "details": result,
        }), 422

    return jsonify({
        "chiSoMoi": float(chi_so_moi),
        "tieuThu": round(float(chi_so_moi - chi_so_cu), 2),
        "confidence": result.get("confidence"),
        "note": result.get("note"),
        "model_used": result.get("model_used", VLM_MODEL),
    }), 200


if __name__ == "__main__":
    port = int(os.getenv("OCR_PORT", "5000"))
    print(f"Python OCR Server đang chạy tại cổng {port}...")
    app.run(host="0.0.0.0", port=port, debug=False)