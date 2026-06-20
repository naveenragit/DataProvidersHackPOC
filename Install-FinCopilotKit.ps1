<#
.SYNOPSIS
    Installs the Financial Services Copilot Kit into a target repository or globally.

.DESCRIPTION
    Install-FinCopilotKit.ps1 copies .github/ copilot customization files
    (agents, instructions, prompts, skills) from the fin-copilot-kit source
    into a target repository, and optionally installs prompts globally to the
    VS Code user prompts folder.

.PARAMETER TargetRepo
    Path to the target git repository. Defaults to current directory.

.PARAMETER GlobalPromptsOnly
    Only installs prompts to the VS Code user prompts folder (no repo changes).

.PARAMETER KitSource
    Path to the fin-copilot-kit source. Defaults to C:\fin-copilot-kit.

.EXAMPLE
    # Install into current repo
    .\Install-FinCopilotKit.ps1

.EXAMPLE
    # Install into a specific repo
    .\Install-FinCopilotKit.ps1 -TargetRepo "C:\Projects\my-fin-app"

.EXAMPLE
    # Install prompts globally only (no repo changes)
    .\Install-FinCopilotKit.ps1 -GlobalPromptsOnly
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $TargetRepo       = (Get-Location).Path,
    [switch] $GlobalPromptsOnly,
    [string] $KitSource        = "C:\fin-copilot-kit"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Colours ──────────────────────────────────────────────────────────────────
function Write-Step   { param([string]$msg) Write-Host "  ► $msg" -ForegroundColor Cyan }
function Write-Ok     { param([string]$msg) Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn   { param([string]$msg) Write-Host "  ⚠ $msg" -ForegroundColor Yellow }
function Write-Header { param([string]$msg) Write-Host "`n$msg" -ForegroundColor White }

# ── Validate source ───────────────────────────────────────────────────────────
if (-not (Test-Path $KitSource)) {
    Write-Error "Copilot kit not found at: $KitSource`nRun this script from the fin-copilot-kit directory or pass -KitSource."
}

Write-Header "═══════════════════════════════════════════════════"
Write-Header " Financial Services Copilot Kit — Installer"
Write-Header "═══════════════════════════════════════════════════"
Write-Host "  Kit source : $KitSource"
Write-Host "  Target repo: $TargetRepo"

# ── Step 1: Global prompts (always) ──────────────────────────────────────────
Write-Header "Step 1: Install prompts globally (VS Code user folder)"
$userPrompts = "$env:APPDATA\Code\User\prompts"
New-Item -ItemType Directory -Force -Path $userPrompts | Out-Null
$promptFiles = Get-ChildItem "$KitSource\.github\prompts\*.prompt.md"
foreach ($f in $promptFiles) {
    Copy-Item $f.FullName $userPrompts -Force
    Write-Ok "  $($f.Name)"
}
Write-Ok "Prompts available in all VS Code projects → $userPrompts"

if ($GlobalPromptsOnly) {
    Write-Header "Done (global prompts only mode)"
    exit 0
}

# ── Step 1b: Install as local VS Code extension (makes AGENTS globally visible) ──
Write-Header "Step 1b: Install as local VS Code extension (agents + instructions + skills)"
$extInstallDir = "$env:USERPROFILE\.vscode\extensions\fin-copilot-kit-1.0.0"
New-Item -ItemType Directory -Force -Path $extInstallDir | Out-Null
@'
{
  "name": "fin-copilot-kit",
  "displayName": "Financial Services Copilot Kit",
  "description": "Agents, prompts, instructions, and skills for Capital Markets, Banking, and Insurance on Azure",
  "version": "1.0.0",
  "publisher": "fin-copilot",
  "engines": { "vscode": "^1.99.0" },
  "categories": ["AI"],
  "contributes": {}
}
'@ | Set-Content "$extInstallDir\package.json" -Encoding UTF8

Get-ChildItem "$KitSource\.github" -Recurse -File | ForEach-Object {
    $dest = $_.FullName.Replace("$KitSource\.github", "$extInstallDir\.github")
    $destDir = Split-Path $dest -Parent
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item $_.FullName $dest -Force
}
Write-Ok "Extension installed → $extInstallDir"
Write-Warn "Reload VS Code (Ctrl+Shift+P → 'Reload Window') to activate agents"

# ── Step 2: Validate target is a git repo ────────────────────────────────────
Write-Header "Step 2: Validate target repository"
if (-not (Test-Path "$TargetRepo\.git") -and -not (Test-Path "$TargetRepo\package.json") -and -not (Test-Path "$TargetRepo\pyproject.toml")) {
    Write-Warn "No .git, package.json, or pyproject.toml found at $TargetRepo"
    Write-Warn "Continuing anyway — make sure this is the correct project root."
}
Write-Ok "Target: $TargetRepo"

# ── Step 3: Create .github structure in target ───────────────────────────────
Write-Header "Step 3: Install .github copilot files into repo"
$githubDest = "$TargetRepo\.github"
$foldersToCreate = @(
    "agents",
    "instructions\coding-standards",
    "instructions\financial-domain",
    "skills\azure-financial-services",
    "skills\workflow-visualization"
)
foreach ($folder in $foldersToCreate) {
    New-Item -ItemType Directory -Force -Path "$githubDest\$folder" | Out-Null
}

# Copy copilot-instructions.md (merge if exists)
$rootInstructions = "$githubDest\copilot-instructions.md"
if (Test-Path $rootInstructions) {
    Write-Warn "copilot-instructions.md already exists — writing to copilot-instructions.fin.md instead"
    Copy-Item "$KitSource\.github\copilot-instructions.md" "$githubDest\copilot-instructions.fin.md" -Force
    Write-Warn "Manually merge copilot-instructions.fin.md into your copilot-instructions.md"
} else {
    Copy-Item "$KitSource\.github\copilot-instructions.md" $rootInstructions -Force
    Write-Ok "copilot-instructions.md"
}

# Agents
Write-Step "Installing agents..."
Get-ChildItem "$KitSource\.github\agents\*.agent.md" | ForEach-Object {
    Copy-Item $_.FullName "$githubDest\agents\" -Force
    Write-Ok "  agents\$($_.Name)"
}

# Instructions
Write-Step "Installing instructions..."
Get-ChildItem "$KitSource\.github\instructions\coding-standards\*.instructions.md" | ForEach-Object {
    Copy-Item $_.FullName "$githubDest\instructions\coding-standards\" -Force
    Write-Ok "  instructions\coding-standards\$($_.Name)"
}
Get-ChildItem "$KitSource\.github\instructions\financial-domain\*.instructions.md" | ForEach-Object {
    Copy-Item $_.FullName "$githubDest\instructions\financial-domain\" -Force
    Write-Ok "  instructions\financial-domain\$($_.Name)"
}

# Skills
Write-Step "Installing skills..."
Copy-Item "$KitSource\.github\skills\azure-financial-services\SKILL.md" "$githubDest\skills\azure-financial-services\" -Force
Write-Ok "  skills\azure-financial-services\SKILL.md"
Copy-Item "$KitSource\.github\skills\workflow-visualization\SKILL.md" "$githubDest\skills\workflow-visualization\" -Force
Write-Ok "  skills\workflow-visualization\SKILL.md"

# ── Step 4: Copy workflow visualization templates ─────────────────────────────
Write-Header "Step 4: Install workflow visualization templates"
$templateDest = "$TargetRepo\templates\workflow-visualization"
New-Item -ItemType Directory -Force -Path $templateDest | Out-Null
Get-ChildItem "$KitSource\templates\workflow-visualization\*" | ForEach-Object {
    Copy-Item $_.FullName $templateDest -Force
    Write-Ok "  templates\workflow-visualization\$($_.Name)"
}

# ── Step 5: Add .copilot-tracking to .gitignore ───────────────────────────────
Write-Header "Step 5: Update .gitignore"
$gitignorePath = "$TargetRepo\.gitignore"
$trackingEntry = ".copilot-tracking/"
if (Test-Path $gitignorePath) {
    $content = Get-Content $gitignorePath -Raw
    if ($content -notmatch [regex]::Escape($trackingEntry)) {
        Add-Content $gitignorePath "`n# GitHub Copilot RPI tracking files`n$trackingEntry"
        Write-Ok "Added .copilot-tracking/ to .gitignore"
    } else {
        Write-Ok ".copilot-tracking/ already in .gitignore"
    }
} else {
    Set-Content $gitignorePath "# GitHub Copilot RPI tracking files`n$trackingEntry`n"
    Write-Ok "Created .gitignore with .copilot-tracking/"
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Header "═══════════════════════════════════════════════════"
Write-Header " Installation Complete!"
Write-Header "═══════════════════════════════════════════════════"
Write-Host ""
Write-Host "  Global (all VS Code projects):"
Write-Host "    Prompts installed to: $userPrompts"
Write-Host ""
Write-Host "  Repository: $TargetRepo"
Write-Host "    .github\copilot-instructions.md"
Write-Host "    .github\agents\  (4 RPI agents)"
Write-Host "    .github\instructions\  (4 instruction files)"
Write-Host "    .github\skills\  (2 skills)"
Write-Host "    templates\workflow-visualization\  (6 React components)"
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor White
Write-Host "  1. Copy templates\workflow-visualization\* to your frontend src/" -ForegroundColor Cyan
Write-Host "  2. In Copilot Chat: /scaffold-financial-app  (new app)" -ForegroundColor Cyan
Write-Host "  3. Or start a feature: /fin-task-research <topic>" -ForegroundColor Cyan
Write-Host ""
