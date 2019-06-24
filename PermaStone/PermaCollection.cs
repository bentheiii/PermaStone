using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using CipherStone;

namespace PermaStone.Enumerable
{
    public class PermaCollection<T> : ICollection<T>, ISafeDeletable
    {
        private readonly PermaLabeledDictionary<T> _int;
        private readonly PermaObject<long> _maxname;
        public PermaCollection(string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate) : this(getFormatter.GetFormatter<T>(), name, deleteOnDispose, access, share, mode) { }
        public PermaCollection(IFormatter<T> serializer, string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate)
        {
            _int = new PermaLabeledDictionary<T>(serializer, name, null, deleteOnDispose, access, share, mode);
            _maxname = new PermaObject<long>(Utility.MutateFileName(name, k => "__COLLECTIONMAXINDEX_" + k), deleteOnDispose, access, share, mode);
        }
        public IEnumerator<T> GetEnumerator()
        {
            return _int.Values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        public void Add(T item)
        {
            BigInteger i = _maxname.value++;
            _int[i.ToString("X")] = item;
        }
        public void Clear()
        {
            _int.Clear();
            _maxname.value = 0;
        }
        public bool Contains(T item)
        {
            return _int.Values.Contains(item);
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            _int.Values.CopyTo(array, arrayIndex);
        }
        public bool Remove(T item)
        {
            foreach (var p in _int)
            {
                if (p.Value.Equals(item))
                {
                    _int.Remove(p);
                    return true;
                }
            }
            return false;
        }
        public int Count
        {
            get
            {
                return _int.Count;
            }
        }
        public bool IsReadOnly
        {
            get
            {
                return _int.IsReadOnly;
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _int.Dispose();
                _maxname.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public bool DeleteOnDispose
        {
            get
            {
                return _int.DeleteOnDispose;
            }
            set
            {
                _int.DeleteOnDispose = value;
                _maxname.DeleteOnDispose = value;
            }
        }
        public FileAccess access
        {
            get
            {
                return _maxname.access;
            }
        }
        public FileShare share
        {
            get
            {
                return _maxname.share;
            }
        }
        public bool AllowCaching
        {
            get
            {
                return _maxname.AllowCaching;
            }
        }
    }
}