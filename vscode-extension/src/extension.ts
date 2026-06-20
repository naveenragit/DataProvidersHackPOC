import * as vscode from 'vscode'
import * as path from 'path'
import * as fs from 'fs'

export function activate(context: vscode.ExtensionContext) {
    // Register the install command
    const installCmd = vscode.commands.registerCommand('finCopilotKit.installToWorkspace', async () => {
        const workspaceFolders = vscode.workspace.workspaceFolders
        if (!workspaceFolders) {
            vscode.window.showErrorMessage('No workspace open. Open a project folder first.')
            return
        }

        const target = workspaceFolders[0].uri.fsPath
        const kitRoot = context.extensionPath

        const confirm = await vscode.window.showInformationMessage(
            `Install Financial Services Copilot Kit into:\n${target}`,
            { modal: true },
            'Install',
        )
        if (confirm !== 'Install') return

        try {
            await installKit(kitRoot, target)
            vscode.window.showInformationMessage(
                '✓ Financial Services Copilot Kit installed! Reload VS Code to activate agents.',
                'Reload Now',
            ).then(action => {
                if (action === 'Reload Now') {
                    vscode.commands.executeCommand('workbench.action.reloadWindow')
                }
            })
        } catch (err) {
            vscode.window.showErrorMessage(`Installation failed: ${err}`)
        }
    })

    context.subscriptions.push(installCmd)
}

async function installKit(kitRoot: string, targetRepo: string): Promise<void> {
    const githubSrc = path.join(kitRoot, '.github')
    const githubDst = path.join(targetRepo, '.github')

    // Folders to create
    const folders = [
        'agents',
        path.join('instructions', 'coding-standards'),
        path.join('instructions', 'financial-domain'),
        path.join('skills', 'azure-financial-services'),
        path.join('skills', 'workflow-visualization'),
    ]
    for (const folder of folders) {
        fs.mkdirSync(path.join(githubDst, folder), { recursive: true })
    }

    // Copy all .github contents recursively
    copyDir(githubSrc, githubDst)

    // Copy templates
    const templatesSrc = path.join(kitRoot, 'templates')
    const templatesDst = path.join(targetRepo, 'templates')
    copyDir(templatesSrc, templatesDst)

    // Add .copilot-tracking/ to .gitignore
    const gitignorePath = path.join(targetRepo, '.gitignore')
    const entry = '\n# GitHub Copilot RPI tracking files\n.copilot-tracking/\n'
    if (fs.existsSync(gitignorePath)) {
        const content = fs.readFileSync(gitignorePath, 'utf8')
        if (!content.includes('.copilot-tracking/')) {
            fs.appendFileSync(gitignorePath, entry)
        }
    } else {
        fs.writeFileSync(gitignorePath, entry)
    }
}

function copyDir(src: string, dst: string): void {
    if (!fs.existsSync(src)) return
    fs.mkdirSync(dst, { recursive: true })
    for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
        const srcPath = path.join(src, entry.name)
        const dstPath = path.join(dst, entry.name)
        if (entry.isDirectory()) {
            copyDir(srcPath, dstPath)
        } else {
            fs.copyFileSync(srcPath, dstPath)
        }
    }
}

export function deactivate() {}
