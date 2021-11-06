using KrunkScriptParser.Validator;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace KrunkScriptLanguageServer
{
    class BufferManager
    {
        private ConcurrentDictionary<string, BufferData> _buffers = new ConcurrentDictionary<string, BufferData>();

        public void UpdateBuffer(string documentPath, BufferData data)
        {
            _buffers.AddOrUpdate(documentPath, data, (k, v) => data);
        }

        public BufferData GetBuffer(string documentPath)
        {
            return _buffers.TryGetValue(documentPath, out BufferData buffer) ? buffer : null;
        }

        public void RemoveBuffer(string documentPath)
        {
            _buffers.TryRemove(documentPath, out BufferData a);
        }
    }
}
