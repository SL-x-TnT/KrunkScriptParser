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
    class Definitionhandler : IDefinitionHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.krnk"
            }
        );

        public Definitionhandler(ILanguageServer router, BufferManager bufferManager)
        {
            _router = router;
            _bufferManager = bufferManager;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = _documentSelector
            };
        }

        public async Task<LocationOrLocations> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            string documentPath = request.TextDocument.Uri.ToString();
            BufferData buffer = _bufferManager.GetBuffer(documentPath);

            if (buffer == null)
            {
                return new LocationOrLocations();
            }

            string text = buffer.GetHoverText((int)request.Position.Line, (int)request.Position.Character);

            List<AutoCompleteSuggestion> suggestions = buffer.Validator.AutoCompleteSuggestions(text, (int)request.Position.Line, (int)request.Position.Character);

            string lastAccess = text.Split('.').Last();

            AutoCompleteSuggestion suggestion = suggestions.FirstOrDefault(x => x.Text == lastAccess);
            DefinitionLocation location = suggestion?.Location;

            //No definition
            if(suggestion?.Location == null || location.StartLocation == null || location.EndLocation == null)
            {
                return null;
            }

            return new LocationOrLocations(new Location
            {
                Uri = String.IsNullOrEmpty(suggestion.FileName) ? request.TextDocument.Uri : new Uri($"file:///{suggestion.FileName}"),
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(location.StartLocation.Line - 1, location.StartLocation.Column - 1),
                    new Position(location.EndLocation.Line - 1, location.EndLocation.ColumnEnd - 1)
                    )
            });
        }

        public void SetCapability(DefinitionCapability capability)
        {
        }
    }

}
