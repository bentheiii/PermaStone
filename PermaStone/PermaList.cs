using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CipherStone;
using WhetStone.Looping;

using static PermaStone.Utility;

namespace PermaStone.Enumerable
{
    public class PermaList<T> : IList<T>, ISafeDeletable
    {
        [Serializable]
        public class PermaObjArrayData
        {
            public int length { get; }
            public int offset { get; }
            public PermaObjArrayData(int length, int offset)
            {
                this.length = length;
                this.offset = offset;
            }
        }
        private IList<IPermaObject<T>> _array;
        private readonly IPermaObject<PermaObjArrayData> _data;
        private readonly IFormatter<T> _serializer;
        public bool DeleteOnDispose { get; set; }
        public FileAccess access
        {
            get
            {
                return _data.access;
            }
        }
        public FileShare share
        {
            get
            {
                return _data.share;
            }
        }
        public bool AllowCaching
        {
            get
            {
                return _data.AllowCaching;
            }
        }
        private readonly T _valueIfCreated;
        public bool SupportMultiAccess => _data.share != FileShare.None;
        public PermaList(string name, int length = 0, int offset = 2, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, T valueIfCreated = default(T), bool allowCaching = true) :
            this(length, offset, getFormatter.GetFormatter<T>(), name, deleteOnDispose, access, share, mode, valueIfCreated, allowCaching) { }
        //if array already exists, the length and offset parameters are ignored
        public PermaList(int length, int offset, IFormatter<T> serializer, string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, T valueIfCreated = default(T), bool allowCaching = true)
        {
            _serializer = serializer;
            this.DeleteOnDispose = deleteOnDispose;
            _valueIfCreated = valueIfCreated;
            this.name = name;
            _data = new PermaObject<PermaObjArrayData>(MutateFileName(name, a => "__ARRAYDATA_" + a), deleteOnDispose, access, share, mode, new PermaObjArrayData(length, offset), allowCaching);
            this.updateArr(true);
        }
        private string getname(int ind)
        {
            return MutateFileName(name, k => "__ARRAYMEMBER_" + ind + "_" + k);
        }
        private IPermaObject<T> getperma(int index)
        {
            return new PermaObject<T>(_serializer, getname(index), DeleteOnDispose, _data.access, _data.share, valueIfCreated: _valueIfCreated,
                allowCaching: _data.AllowCaching);
        }
        private void updateArr(bool overridemulti = false)
        {
            if (SupportMultiAccess || overridemulti)
            {
                _array?.Do(a => { a.DeleteOnDispose = false; a.Dispose(); });
                this._array = range.Range(this._data.value.offset, _data.value.offset + _data.value.length).Select(getperma).ToList();
            }
        }
        public int IndexOf(T item)
        {
            return (_array.CountBind().Cast<(IPermaObject<T>, int)?>().FirstOrDefault(a => a.Value.Item1.value.Equals(item), null) ?? ((IPermaObject<T>)null, -1)).Item2;
        }
        public void Insert(int index, T item)
        {
            updateArr();
            if (index < 0 || index > length)
                throw new ArgumentOutOfRangeException(nameof(index), "out of range");
            if (index == 0 && length > 0)
            {
                if (_data.value.offset <= 0)
                    offsetfiles(this.length / 4);
                _data.MutauteValue(a => new PermaObjArrayData(a.length + 1, a.offset - 1));
                IPermaObject<T> newval = getperma(_data.value.offset);
                newval.value = item;
                _array.Insert(0, newval);
            }
            else
            {
                _data.MutauteValue(a => new PermaObjArrayData(a.length + 1, a.offset));
                updateArr(true);
                foreach (var i in range.Range(index, this.length - 1).Reverse())
                {
                    this[i + 1] = this[i];
                }
                this[index] = item;
            }
        }
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException(nameof(index), "out of range");
            updateArr();
            if (index == 0)
            {
                var todel = this._array[0];
                todel.DeleteOnDispose = true;
                todel.Dispose();
                _array.RemoveAt(0);
                _data.MutauteValue(a => new PermaObjArrayData(a.length - 1, a.offset + 1));
            }
            else
            {
                foreach (var i in range.Range(index, this.length - 1))
                {
                    this[i] = this[i + 1];
                }
                this._array.Last().DeleteOnDispose = true;
                this._array.Last().Dispose();
                this._array.RemoveAt(this._array.Count - 1);
                _data.MutauteValue(a => new PermaObjArrayData(a.length - 1, a.offset));
            }
        }
        public T this[int i]
        {
            get
            {
                this.updateArr();
                if (i < 0 || i >= _data.value.length)
                    throw new ArgumentOutOfRangeException("index " + i + " is outside bounds of permaArray");
                return _array[i].value;
            }
            set
            {
                this.updateArr();
                if (i < 0 || i >= _data.value.length)
                    throw new ArgumentOutOfRangeException("index " + i + " is outside bounds of permaArray");
                _array[i].value = value;
            }
        }
        public void MutauteValue(int i, Func<T, T> mutation)
        {
            this.updateArr();
            _array[i].MutauteValue(mutation);
        }
        public T tryParse(int i, out Exception ex)
        {
            this.updateArr();
            return _array[i].tryParse(out ex);
        }
        public string name { get; }
        public string LocalName() => _data.LocalName();
        public IEnumerator<T> GetEnumerator()
        {
            this.updateArr();
            return _array.Select(a => a.value).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }
        protected virtual void Dispose(bool disposing)
        {
            this.updateArr();
            if (disposing)
            {
                foreach (IPermaObject<T> iPermaObject in _array)
                {
                    iPermaObject.DeleteOnDispose = this.DeleteOnDispose;
                    iPermaObject.Dispose();
                }
                _data.DeleteOnDispose = this.DeleteOnDispose;
                _data.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void offsetfiles(int offset)
        {
            updateArr();
            foreach (IPermaObject<T> iPermaObject in _array)
            {
                iPermaObject.DeleteOnDispose = false;
                iPermaObject.Dispose();
            }
            foreach (var fileindex in range.Range(_data.value.offset, _data.value.offset + _data.value.length).Reverse())
            {
                File.Move(getname(fileindex), getname(fileindex + offset));
            }
            _data.MutauteValue(a => new PermaObjArrayData(a.length, a.offset + offset));
            updateArr(true);
        }
        public int length
        {
            get
            {
                this.updateArr();
                return _data.value.length;
            }
        }
        public void Add(T item)
        {
            Insert(length, item);
        }
        public void Clear()
        {
            updateArr();
            foreach (var iPermaObject in _array)
            {
                iPermaObject.DeleteOnDispose = true;
                iPermaObject.Dispose();
            }
            _data.value = new PermaObjArrayData(0, 0);
            updateArr(true);
        }
        public bool Contains(T item)
        {
            return IndexOf(item) > 0;
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (var t in _array.CountBind(arrayIndex))
            {
                array[t.Item2] = t.Item1.value;
            }
        }
        public bool Remove(T item)
        {
            var i = IndexOf(item);
            if (i < 0)
                return false;
            RemoveAt(i);
            return true;
        }
        public int Count
        {
            get
            {
                return _data.value.length;
            }
        }
        public bool IsReadOnly => false;
    }
}
