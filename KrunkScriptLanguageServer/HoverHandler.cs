using KrunkScriptParser.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KrunkScriptLanguageServer
{
    class HoverHandler : IHoverHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.krnk"
            }
        );

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions
            {
                DocumentSelector = _documentSelector,
                SyncKind = TextDocumentSyncKind.Full
            };
        }

        public HoverHandler(ILanguageServer router, BufferManager bufferManager)
        {
            _router = router;
            _bufferManager = bufferManager;
        }

        public async Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            string documentPath = request.TextDocument.Uri.ToString();
            BufferData buffer = _bufferManager.GetBuffer(documentPath);

            if (buffer == null)
            {
                return new Hover();
            }

            string text = buffer.GetHoverText((int)request.Position.Line, (int)request.Position.Character);

            List<AutoCompleteSuggestion> suggestions = buffer.Validator.AutoCompleteSuggestions(text, (int)request.Position.Line, (int)request.Position.Character);

            string lastAccess = text.Split('.').Last();

            suggestions = suggestions.Where(x => x.Text == lastAccess).ToList();

            if(suggestions.Count != 1)
            {
                return new Hover();
            }

            List<MarkedString> hoverData = new List<MarkedString> { new MarkedString(suggestions[0].Details) };

            if(suggestions[0].Documentation != null)
            {
                hoverData.Add(new MarkedString(suggestions[0].Documentation));
            }

            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(hoverData)
            };
        }

        public void SetCapability(HoverCapability capability)
        {

        }
    }

}
