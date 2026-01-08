param(
  [Parameter(Mandatory = $false)]
  [string]$Root = "Assets",

  [Parameter(Mandatory = $false)]
  [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Strip-CSharpComments([string]$text) {
  $sb = New-Object System.Text.StringBuilder

  $state = 'Code' # Code | LineComment | BlockComment | String | VerbatimString | Char
  $i = 0

  while ($i -lt $text.Length) {
    $c = $text[$i]
    $n = if ($i + 1 -lt $text.Length) { $text[$i + 1] } else { [char]0 }

    switch ($state) {
      'Code' {
        if ($c -eq '/' -and $n -eq '/') {
          $state = 'LineComment'
          $i += 2
          continue
        }
        if ($c -eq '/' -and $n -eq '*') {
          $state = 'BlockComment'
          $i += 2
          continue
        }
        if ($c -eq '@' -and $n -eq '"') {
          [void]$sb.Append($c)
          [void]$sb.Append($n)
          $state = 'VerbatimString'
          $i += 2
          continue
        }
        if ($c -eq '"') {
          [void]$sb.Append($c)
          $state = 'String'
          $i++
          continue
        }
        if ($c -eq "'") {
          [void]$sb.Append($c)
          $state = 'Char'
          $i++
          continue
        }

        [void]$sb.Append($c)
        $i++
        continue
      }

      'LineComment' {
        if ($c -eq "`r" -or $c -eq "`n") {
          [void]$sb.Append($c)
          $state = 'Code'
        }
        $i++
        continue
      }

      'BlockComment' {
        if ($c -eq '*' -and $n -eq '/') {
          $state = 'Code'
          $i += 2
          continue
        }
        $i++
        continue
      }

      'String' {
        if ($c -eq '\\') {
          [void]$sb.Append($c)
          if ($i + 1 -lt $text.Length) {
            [void]$sb.Append($text[$i + 1])
            $i += 2
          } else {
            $i++
          }
          continue
        }
        [void]$sb.Append($c)
        if ($c -eq '"') { $state = 'Code' }
        $i++
        continue
      }

      'VerbatimString' {
        [void]$sb.Append($c)
        if ($c -eq '"') {
          if ($n -eq '"') {
            [void]$sb.Append($n)
            $i += 2
            continue
          }
          $state = 'Code'
        }
        $i++
        continue
      }

      'Char' {
        if ($c -eq '\\') {
          [void]$sb.Append($c)
          if ($i + 1 -lt $text.Length) {
            [void]$sb.Append($text[$i + 1])
            $i += 2
          } else {
            $i++
          }
          continue
        }
        [void]$sb.Append($c)
        if ($c -eq "'") { $state = 'Code' }
        $i++
        continue
      }
    }
  }

  return $sb.ToString()
}

$rootPath = Resolve-Path $Root
$files = Get-ChildItem -Path $rootPath -Recurse -File -Filter *.cs

$changed = 0
foreach ($f in $files) {
  $original = Get-Content -LiteralPath $f.FullName -Raw
  $stripped = Strip-CSharpComments $original

  if ($stripped -ne $original) {
    $changed++
    if (-not $WhatIf) {
      Set-Content -LiteralPath $f.FullName -Value $stripped -NoNewline
    }
  }
}

Write-Host "Processed $($files.Count) .cs files under '$Root'. Changed: $changed. WhatIf: $WhatIf"