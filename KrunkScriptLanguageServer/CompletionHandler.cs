using KrunkScriptParser.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace KrunkScriptLanguageServer
{
    class CompletionHandler : ICompletionHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.krnk"
            }
        );

        private CompletionCapability _capability;

        public CompletionHandler(ILanguageServer router, BufferManager bufferManager)
        {
            _router = router;
            _bufferManager = bufferManager;
        }

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = false,
                TriggerCharacters = new Container<string> (".")
            };
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            string documentPath = request.TextDocument.Uri.ToString();
            BufferData buffer = _bufferManager.GetBuffer(documentPath);

            if(buffer == null)
            {
                return new CompletionList();
            }

            string text = buffer.GetText((int)request.Position.Line, (int)request.Position.Character);

            List<AutoCompleteSuggestion> suggestions = buffer.Validator.AutoCompleteSuggestions(text, (int)request.Position.Line, (int)request.Position.Character);

            CompletionList completionList = new CompletionList(suggestions.Select(suggestion =>
            {
                return new CompletionItem
                {
                    Label = suggestion.Text,
                    Detail = suggestion.Details,
                    Kind = (CompletionItemKind)suggestion.Type,
                    InsertTextFormat = !String.IsNullOrEmpty(suggestion.InsertTextFormat) ? InsertTextFormat.Snippet : InsertTextFormat.PlainText,
                    InsertText = suggestion.InsertTextFormat,
                    Documentation = suggestion.Documentation
                };
            }), suggestions.Count > 1);

            return completionList;
        }

        public void SetCapability(CompletionCapability capability)
        {
            _capability = capability;
        }
    }
}
