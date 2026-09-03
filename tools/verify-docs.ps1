[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$documents = @(
    Get-Item -LiteralPath (Join-Path $repositoryRoot "README.md")
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "docs") -Recurse -File -Filter "*.md"
)
$errors = [System.Collections.Generic.List[string]]::new()
$linkPattern = [regex]'!?\[[^\]]*\]\(([^)]+)\)'

foreach ($document in $documents) {
    $content = Get-Content -LiteralPath $document.FullName -Raw

    if ($content.Contains("file:///")) {
        $errors.Add("$($document.FullName): zawiera lokalny link file:///")
    }

    foreach ($match in $linkPattern.Matches($content)) {
        $target = $match.Groups[1].Value.Trim()
        if ($target.StartsWith("<") -and $target.EndsWith(">")) {
            $target = $target.Substring(1, $target.Length - 2)
        }

        if ([string]::IsNullOrWhiteSpace($target) -or
            $target.StartsWith("#") -or
            $target -match '^(https?|mailto):') {
            continue
        }

        $pathPart = $target.Split("#", 2)[0]
        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            continue
        }

        $candidate = Join-Path $document.DirectoryName $pathPart
        if (-not (Test-Path -LiteralPath $candidate)) {
            $relativeDocument = [IO.Path]::GetRelativePath($repositoryRoot, $document.FullName)
            $errors.Add("$($relativeDocument): nie istnieje cel linku '$($target)'")
        }
    }
}

$managedDirectories = @("tutorials", "how-to", "explanation", "reference", "report")
foreach ($directory in $managedDirectories) {
    $path = Join-Path (Join-Path $repositoryRoot "docs") $directory
    foreach ($file in Get-ChildItem -LiteralPath $path -File -Filter "*.md") {
        if ($file.BaseName -cnotmatch '^[0-9a-z]+(?:-[0-9a-z]+)*$') {
            $errors.Add("$($file.FullName): nazwa pliku nie jest lowercase-kebab-case")
        }
    }
}

if ($errors.Count -gt 0) {
    $separator = [Environment]::NewLine + "- "
    Write-Error ("Walidacja dokumentacji nie powiodła się:" + $separator + ($errors -join $separator))
}

Write-Host "Dokumentacja: sprawdzono $($documents.Count) plików; linki lokalne są poprawne."

