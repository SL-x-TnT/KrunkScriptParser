'use strict';

import { workspace, Disposable, ExtensionContext, window } from 'vscode';
import { LanguageClient, LanguageClientOptions, SettingMonitor, ServerOptions, TransportKind, InitializeParams } from 'vscode-languageclient/node';
import { Trace } from 'vscode-jsonrpc';
import * as path from 'path';

let client: LanguageClient;

export function activate(context: ExtensionContext) {

    // The server is implemented in node
    let serverExe = 'dotnet';
    let dllLocation = context.asAbsolutePath(path.join('server', 'KrunkScriptLanguageServer.dll'));

    // If the extension is launched in debug mode then the debug server options are used
    // Otherwise the run options are used
    let serverOptions: ServerOptions = {
        run: { command: serverExe, args: [dllLocation]},//['C:\\Users\\TnT\\source\\repos\\KrunkerScript\\KrunkScriptLanguageServer\\bin\\Debug\\netcoreapp3.1\\KrunkScriptLanguageServer.dll'] },
        debug: { command: serverExe, args: [dllLocation]}//['C:\\Users\\TnT\\source\\repos\\KrunkerScript\\KrunkScriptLanguageServer\\bin\\Debug\\netcoreapp3.1\\KrunkScriptLanguageServer.dll']  }
    };

    
    // Options to control the language client
    let clientOptions: LanguageClientOptions = {
        documentSelector: [
            {
                pattern: '**/*.krnk',
            }
        ],
        synchronize: {
            fileEvents: workspace.createFileSystemWatcher('**/*.krnk')
        },
    };

    // Create the language client and start the client.
    client = new LanguageClient('krnkServer', 'KrunkScript Server', serverOptions, clientOptions);
    client.trace = Trace.Verbose;

    let disposable = client.start();

    // Push the disposable to the context's subscriptions so that the
    // client can be deactivated on extension deactivation
    context.subscriptions.push(disposable);
}

export function deactivate(): Thenable<void> | undefined {
    if (!client) {
      return undefined;
    }
    return client.stop();
  }