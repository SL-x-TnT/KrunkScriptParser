﻿using KrunkScriptParser.Models;
using KrunkScriptParser.Validator;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KrunkScriptLanguageServer
{
    internal class KrnkDocumentSyncHandler : ITextDocumentSyncHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.krnk"
            }
        );

        private SynchronizationCapability _capability;

        public KrnkDocumentSyncHandler(ILanguageServer router, BufferManager bufferManager)
        {
            _router = router;
            _bufferManager = bufferManager;
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        private void ValidateText(Uri uri, string text)
        {
            KSValidator validator = new KSValidator(text);

            validator.Validate();

            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray<Diagnostic>.Empty.ToBuilder();

            foreach (ValidationException error in validator.ValidationExceptions)
            {
                Position startPosition = new Position(error.LineStart - 1, error.ColumnStart - 1);
                Position endPosition = new Position(error.LineEnd - 1, error.ColumnEnd - 1);

                if (startPosition.Line < 0) { startPosition.Line = 0; };
                if (startPosition.Character < 0) { startPosition.Character = 0; };


                if (endPosition.Line < 0) { endPosition.Line = 0; };
                if (endPosition.Character < 0) { endPosition.Character = 0; };


                diagnostics.Add(new Diagnostic
                {
                    Severity = (DiagnosticSeverity)error.Level,
                    Message = error.Message,
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(startPosition, endPosition)
                });
            }

            _router.Document.PublishDiagnostics(new PublishDiagnosticsParams()
            {
                Diagnostics = new Container<Diagnostic>(diagnostics.ToArray()),
                Uri = uri
            });
        }

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Change
            };
        }

        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            return new TextDocumentAttributes(uri, "krnk");
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var documentPath = request.TextDocument.Uri.ToString();
            var text = request.ContentChanges.FirstOrDefault()?.Text;

            _bufferManager.UpdateBuffer(documentPath, new StringBuilder(text));

            ValidateText(request.TextDocument.Uri, text);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            _bufferManager.UpdateBuffer(request.TextDocument.Uri.ToString(), new StringBuilder(request.TextDocument.Text));
            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = true
            };
        }
    }
}