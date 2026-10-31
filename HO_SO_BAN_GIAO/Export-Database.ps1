param(
    [Parameter(Mandatory=$true)][string]$SourceDatabase,
    [Parameter(Mandatory=$true)][string]$OutputFile,
    [string]$TargetDatabase = 'KLCN052_QuanLyPhongTro',
    [string]$ServerInstance = '.\SQLEXPRESS'
)
$ErrorActionPreference = 'Stop'
if ($SourceDatabase -notmatch '^QLPT_Export_[0-9_]+$') { throw 'Export only from an explicitly restored handover snapshot.' }
if ($TargetDatabase -notmatch '^[A-Za-z][A-Za-z0-9_]+$') { throw 'Invalid target name.' }
$lib = 'C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
public static class SmoDependencyResolver {
    public static string Lib, Shared;
    public static void Install() { AppDomain.CurrentDomain.AssemblyResolve += Resolve; }
    static Assembly Resolve(object sender, ResolveEventArgs args) {
        string name = new AssemblyName(args.Name).Name;
        if (name.EndsWith(".resources")) return null;
        string path = Path.Combine(Lib, name + ".dll");
        if (File.Exists(path)) return Assembly.LoadFrom(path);
        string folder = Path.Combine(Shared, name);
        if (Directory.Exists(folder)) foreach (string file in Directory.GetFiles(folder, name + ".dll", SearchOption.AllDirectories))
            if (file.IndexOf("netfx", StringComparison.OrdinalIgnoreCase) >= 0) return Assembly.LoadFrom(file);
        return null;
    }
}
'@
[SmoDependencyResolver]::Lib = $lib
[SmoDependencyResolver]::Shared = 'C:\Program Files\Microsoft SQL Server Management Studio 22\Release\SharedAssemblies'
[SmoDependencyResolver]::Install()
foreach ($name in @('Microsoft.SqlServer.Management.Sdk.Sfc','Microsoft.SqlServer.ConnectionInfo','Microsoft.SqlServer.Smo','Microsoft.SqlServer.SmoExtended','Microsoft.SqlServer.SqlEnum')) {
    [void][System.Reflection.Assembly]::LoadFrom((Join-Path $lib ($name + '.dll')))
}
$server = New-Object Microsoft.SqlServer.Management.Smo.Server $ServerInstance
$server.ConnectionContext.TrustServerCertificate = $true
$server.ConnectionContext.StatementTimeout = 180
$db = $server.Databases[$SourceDatabase]
if ($null -eq $db) { throw 'Snapshot database missing.' }
$outputPath = [IO.Path]::GetFullPath($OutputFile)
if (Test-Path -LiteralPath $outputPath) { throw 'Refusing to overwrite existing snapshot script.' }
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath))
$writer = New-Object IO.StreamWriter($outputPath, $false, (New-Object Text.UTF8Encoding($true)))
function Write-Batch($batch) { $writer.WriteLine([string]$batch); $writer.WriteLine('GO') }
try {
    $writer.WriteLine('-- QLPT full schema + data snapshot. Contains account hashes and tenant information; share privately.')
    $writer.WriteLine('-- Existing database is never overwritten. Change target database name consistently to restore separately.')
    $writer.WriteLine('-- Source snapshot: ' + $SourceDatabase)
    Write-Batch 'USE [master];'
    Write-Batch ("IF DB_ID(N'$TargetDatabase') IS NOT NULL BEGIN RAISERROR(N'Target database already exists. Stop: do not overwrite.',16,1); SET NOEXEC ON; END;")
    Write-Batch ("CREATE DATABASE [$TargetDatabase] COLLATE " + $db.Collation + ';')
    Write-Batch ("USE [$TargetDatabase];")
    Write-Batch 'SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF; SET XACT_ABORT ON;'
    Write-Batch 'BEGIN TRANSACTION;'
    $options = New-Object Microsoft.SqlServer.Management.Smo.ScriptingOptions
    $options.IncludeDatabaseContext = $false
    $options.SchemaQualify = $true
    $options.DriPrimaryKey = $true; $options.DriUniqueKeys = $true
    $options.DriChecks = $true; $options.DriDefaults = $true
    $options.Indexes = $true; $options.ExtendedProperties = $true
    $options.ScriptData = $false; $options.ScriptSchema = $true
    $tables = @($db.Tables | Where-Object { -not $_.IsSystemObject -or $_.Name -eq 'sysdiagrams' } | Sort-Object Schema,Name)
    foreach ($table in $tables) { foreach ($batch in $table.Script($options)) { Write-Batch $batch } }
    $scripter = New-Object Microsoft.SqlServer.Management.Smo.Scripter $server
    $scripter.Options.ScriptData = $true
    $scripter.Options.ScriptSchema = $false
    $scripter.Options.IncludeDatabaseContext = $false
    $scripter.Options.SchemaQualify = $true
    foreach ($table in $tables) {
        foreach ($batch in $scripter.EnumScript([Microsoft.SqlServer.Management.Sdk.Sfc.Urn[]]@($table.Urn))) { Write-Batch $batch }
        if (@($table.Columns | Where-Object { $_.Identity }).Count -gt 0) {
            $safeTable = '[' + $table.Schema.Replace(']',']]') + '].[' + $table.Name.Replace(']',']]') + ']'
            $identity = $db.ExecuteWithResults("SELECT IDENT_CURRENT(N'$safeTable') AS Value").Tables[0].Rows[0].Value
            if ($identity -ne [DBNull]::Value -and $table.RowCount -gt 0) {
                Write-Batch ("DBCC CHECKIDENT (N'$safeTable', RESEED, $identity) WITH NO_INFOMSGS;")
            }
        }
    }
    foreach ($table in $tables) {
        foreach ($fk in $table.ForeignKeys) { foreach ($batch in $fk.Script()) { Write-Batch $batch } }
        foreach ($trigger in $table.Triggers) { if (-not $trigger.IsSystemObject) { foreach ($batch in $trigger.Script()) { Write-Batch $batch } } }
    }
    $objects = $db.ExecuteWithResults("SELECT SCHEMA_NAME(schema_id) AS SchemaName,name,type FROM sys.objects WHERE is_ms_shipped=0 AND type IN ('FN','IF','TF','V','P') ORDER BY type,name").Tables[0]
    foreach ($row in $objects.Rows) {
        if ($row.type.Trim() -eq 'P') { $obj = $db.StoredProcedures[$row.name,$row.SchemaName] }
        elseif ($row.type.Trim() -eq 'V') { $obj = $db.Views[$row.name,$row.SchemaName] }
        else { $obj = $db.UserDefinedFunctions[$row.name,$row.SchemaName] }
        foreach ($batch in $obj.Script()) { Write-Batch $batch }
    }
    foreach ($trigger in $db.Triggers) { if (-not $trigger.IsSystemObject) { foreach ($batch in $trigger.Script()) { Write-Batch $batch } } }
    Write-Batch 'COMMIT;'
    Write-Batch 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;'
    Write-Batch 'SET NOEXEC OFF;'
} finally { $writer.Dispose(); $server.ConnectionContext.Disconnect() }
Get-Item -LiteralPath $outputPath | Select-Object Name,Length
Write-Output ('SHA256: ' + (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash)
