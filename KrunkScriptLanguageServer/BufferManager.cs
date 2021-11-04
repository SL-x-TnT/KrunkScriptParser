
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrunkScriptLanguageServer
{
    class BufferManager
    {
        private ConcurrentDictionary<string, StringBuilder> _buffers = new ConcurrentDictionary<string, StringBuilder>();

        public void UpdateBuffer(string documentPath, StringBuilder buffer)
        {
            _buffers.AddOrUpdate(documentPath, buffer, (k, v) => buffer);
        }

        public StringBuilder GetBuffer(string documentPath)
        {
            return _buffers.TryGetValue(documentPath, out var buffer) ? buffer : null;
        }

        public void RemoveBuffer(string documentPath)
        {
            _buffers.TryRemove(documentPath, out StringBuilder _);
        }
    }
}
