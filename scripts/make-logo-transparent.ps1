# Rebuild status-guard-logo.png from the JPG via edge flood-fill (cream/white -> alpha).
# Requires .NET 8 SDK. Keeps interior near-white (lamp, walls) because only edge-connected bg is cleared.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [int]$Fuzz = 32
)

$ErrorActionPreference = "Stop"
$tmp = Join-Path $env:TEMP ("logo-alpha-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null

try {
    Set-Content -Path (Join-Path $tmp "LogoAlpha.csproj") -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
</Project>
"@

    Set-Content -Path (Join-Path $tmp "Program.cs") -Value @'
using System.Drawing;
using System.Drawing.Imaging;

if (args.Length < 2) { Console.Error.WriteLine("usage: in out [fuzz]"); return 2; }
var input = args[0];
var output = args[1];
var fuzz = args.Length > 2 ? int.Parse(args[2]) : 30;

using var src = new Bitmap(input);
var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
using (var g = Graphics.FromImage(bmp)) g.DrawImage(src, 0, 0, src.Width, src.Height);

var w = bmp.Width;
var h = bmp.Height;
var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
try
{
    unsafe
    {
        byte* basePtr = (byte*)data.Scan0;
        int stride = data.Stride;
        Color Sample(int x, int y)
        {
            var p = basePtr + y * stride + x * 4;
            return Color.FromArgb(p[3], p[2], p[1], p[0]);
        }
        void Clear(int x, int y)
        {
            var p = basePtr + y * stride + x * 4;
            p[0] = p[1] = p[2] = p[3] = 0;
        }

        var c0 = Sample(0, 0);
        bool Near(Color c) =>
            Math.Abs(c.R - c0.R) <= fuzz &&
            Math.Abs(c.G - c0.G) <= fuzz &&
            Math.Abs(c.B - c0.B) <= fuzz;

        var visited = new bool[w * h];
        var q = new Queue<int>();
        void Enq(int x, int y)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
            var i = y * w + x;
            if (visited[i]) return;
            if (!Near(Sample(x, y))) return;
            visited[i] = true;
            q.Enqueue(i);
        }

        for (int x = 0; x < w; x++) { Enq(x, 0); Enq(x, h - 1); }
        for (int y = 0; y < h; y++) { Enq(0, y); Enq(w - 1, y); }

        int cleared = 0;
        while (q.Count > 0)
        {
            var i = q.Dequeue();
            Clear(i % w, i / w);
            cleared++;
            var x = i % w; var y = i / w;
            Enq(x + 1, y); Enq(x - 1, y); Enq(x, y + 1); Enq(x, y - 1);
        }
        Console.WriteLine($"cleared={cleared} corner={c0.R},{c0.G},{c0.B}");
    }
}
finally { bmp.UnlockBits(data); }

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
bmp.Save(output, ImageFormat.Png);
Console.WriteLine($"saved {output} cornerA={bmp.GetPixel(0,0).A}");
bmp.Dispose();
return 0;
'@

    dotnet build (Join-Path $tmp "LogoAlpha.csproj") -v q | Out-Host
    $exe = Get-ChildItem (Join-Path $tmp "bin") -Recurse -Filter LogoAlpha.exe | Select-Object -First 1
    if (-not $exe) { throw "LogoAlpha.exe not built" }

    $pairs = @(
        @{ In = Join-Path $RepoRoot "src\VrchatPrivacy.App\Assets\status-guard-logo.jpg"; Out = Join-Path $RepoRoot "src\VrchatPrivacy.App\Assets\status-guard-logo.png" },
        @{ In = Join-Path $RepoRoot "docs\assets\status-guard-logo.jpg"; Out = Join-Path $RepoRoot "docs\assets\status-guard-logo.png" }
    )
    foreach ($pair in $pairs) {
        & $exe.FullName $pair.In $pair.Out $Fuzz
        if ($LASTEXITCODE -ne 0) { throw "LogoAlpha failed for $($pair.In)" }
    }
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
