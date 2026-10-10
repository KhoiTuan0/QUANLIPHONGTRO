using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
    {
        // Các entity sinh từ scaffold khai báo navigation kiểu non-nullable
        // (vd: Phong.MaLoaiPhongNavigation = null!). Mặc định ASP.NET coi mọi
        // reference type non-nullable là bắt buộc, nên client buộc phải gửi cả
        // object navigation — điều không thể làm được. Hậu quả: mọi POST/PUT
        // bind thẳng entity đều trả 400. Tắt quy ước này để chỉ còn ràng buộc
        // nào được khai báo tường minh bằng [Required].
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<Klcn052QuanLyPhongTroContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
