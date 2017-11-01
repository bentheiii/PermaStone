using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CipherStone;
using WhetStone.Looping;
using WhetStone.WordPlay;
using static PermaStone.Utility;

namespace PermaStone.Enumerable
{
    public class PermaDictionary<K, V> : IDictionary<K, V>, ISafeDeletable
    {
        [Serializable]
        private class PermaDictionaryData
        {
            public PermaDictionaryData(int nextname, string definitions)
            {
                this.nextname = nextname;
                this.definitions = definitions;
            }
            public int nextname { get; }
            public string definitions { get; }
        }
        private readonly PermaObject<PermaDictionaryData> _data;
        private IDictionary<K, IPermaObject<V>> _dic;

        private readonly ByteSerializer _kSerializer;
        private readonly ByteSerializer _vSerializer;
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
        public bool allowCaching { get; }
        private readonly V _vvalueIfCreated;
        public PermaDictionary(string name, bool allowCaching = true, FileAccess access = FileAccess.ReadWrite, bool deleteOnDispose = false, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, V vvalueIfCreated = default(V)) : this(new DotNetSerializer(), new DotNetSerializer(), name, allowCaching, access, deleteOnDispose, share, mode, vvalueIfCreated) { }
        public PermaDictionary(ByteSerializer kSerializer, ByteSerializer vSerializer, string name, bool allowCaching, FileAccess access = FileAccess.ReadWrite, bool deleteOnDispose = false, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, V vvalueIfCreated = default(V))
        {
            this.allowCaching = allowCaching;
            this.DeleteOnDispose = deleteOnDispose;
            _kSerializer = kSerializer;
            _vSerializer = vSerializer;
            _vvalueIfCreated = vvalueIfCreated;
            _data = new PermaObject<PermaDictionaryData>(MutateFileName(name, x => "__DICTIONARY_DATA_" + x), deleteOnDispose, access, share, mode, new PermaDictionaryData(0, ""), allowCaching);
            LoadDictionary(true);
        }
        public bool SupportMultiAccess => _data.share != FileShare.None;

        private IPermaObject<V> getVPerma(string name)
        {
            return new PermaObject<V>(_vSerializer, Path.Combine(Path.GetDirectoryName(_data.name), "__DICTIONARYVALUE_" + name), DeleteOnDispose, _data.access, _data.share, valueIfCreated: _vvalueIfCreated, allowCaching: _data.AllowCaching);
        }
        private void LoadDictionary(bool @override = false)
        {
            if (!SupportMultiAccess && !@override)
                return;
            string val = _data.value.definitions;
            List<string> split = new List<string>();
            while (val != "")
            {
                split.Add(NumberSerialization.FullCodeSerializer.DecodeSpecifiedLength(val, out val));
            }
            _dic = split.Chunk(2).Select(a => Tuple.Create(_kSerializer.deserialize<K>(a[0].Select(x => (byte)x).ToArray()), getVPerma(a[1]))).ToDictionary();
        }
        private string SaveDictionaryToString()
        {
            return
                _dic.SelectMany(
                        a => new[] { NumberSerialization.FullCodeSerializer.EncodeSpecificLength(_kSerializer.serialize(a.Key).Select(x => (char)x).ConvertToString()), NumberSerialization.FullCodeSerializer.EncodeSpecificLength(a.Value.name) })
                    .StrConcat("");
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            LoadDictionary();
            return _dic.Select(a => new KeyValuePair<K, V>(a.Key, a.Value.value)).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(KeyValuePair<K, V> item)
        {
            LoadDictionary();
            if (!_dic.ContainsKey(item.Key))
            {
                _dic.Add(item.Key, getVPerma(NumberSerialization.AlphaNumericSerializer.ToString((ulong)(_data.value.nextname + 1)).Reverse().ConvertToString()));
                _data.MutauteValue(a => new PermaDictionaryData(a.nextname + 1, SaveDictionaryToString()));
            }
            _dic[item.Key].value = item.Value;
        }
        public void Clear()
        {
            LoadDictionary();
            foreach (var permaObject in _dic.Values)
            {
                permaObject.DeleteOnDispose = true;
                permaObject.Dispose();
            }
            _data.value = new PermaDictionaryData(0, "");
            _dic.Clear();
        }
        public bool Contains(KeyValuePair<K, V> item)
        {
            LoadDictionary();
            return _dic.ContainsKey(item.Key) && _dic[item.Key].value.Equals(item.Value);
        }
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            LoadDictionary();
            foreach (var t in _dic.CountBind())
            {
                array[t.Item2] = new KeyValuePair<K, V>(t.Item1.Key, t.Item1.Value.value);
            }
        }
        public bool Remove(KeyValuePair<K, V> item)
        {
            LoadDictionary();
            if (!Contains(item))
                return false;
            Remove(item.Key);
            return true;
        }
        public int Count
        {
            get
            {
                LoadDictionary();
                return _dic.Count;
            }
        }
        public bool IsReadOnly
        {
            get
            {
                return _data.access == FileAccess.Read;
            }
        }
        public bool ContainsKey(K key)
        {
            LoadDictionary();
            return _dic.ContainsKey(key);
        }
        public void Add(K key, V value)
        {
            Add(new KeyValuePair<K, V>(key, value));
        }
        public bool Remove(K key)
        {
            LoadDictionary();
            if (!_dic.ContainsKey(key))
                return false;
            var torem = _dic[key];
            torem.DeleteOnDispose = true;
            torem.Dispose();
            _dic.Remove(key);
            _data.MutauteValue(a => new PermaDictionaryData(a.nextname, SaveDictionaryToString()));
            return true;
        }
        public bool TryGetValue(K key, out V value)
        {
            if (ContainsKey(key))
            {
                value = _dic[key].value;
                return true;
            }
            value = default(V);
            return false;
        }
        public V this[K key]
        {
            get
            {
                if (!TryGetValue(key, out V ret))
                    throw new ArgumentOutOfRangeException(nameof(key));
                return ret;
            }
            set
            {
                Add(key, value);
            }
        }
        public ICollection<K> Keys
        {
            get
            {
                return _dic.Keys;
            }
        }
        public ICollection<V> Values
        {
            get
            {
                return new List<V>(_dic.Values.Select(a => a.value));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public void Dispose(bool disposing)
        {
            if (!disposing)
                return;
            _data.DeleteOnDispose = this.DeleteOnDispose;
            _data.Dispose();
            foreach (var iPermaObject in _dic.Values)
            {
                iPermaObject.DeleteOnDispose = DeleteOnDispose;
                iPermaObject.Dispose();
            }
        }
    }
    public class PermaLabeledDictionary<T> : ISafeDeletable, IDictionary<string, T>
    {
        private readonly IPermaObject<string> _definitions;
        private IDictionary<string, IPermaObject<T>> _dic;
        public bool DeleteOnDispose { get; set; }
        public FileAccess access
        {
            get
            {
                return _definitions.access;
            }
        }
        public FileShare share
        {
            get
            {
                return _definitions.share;
            }
        }
        public bool AllowCaching
        {
            get
            {
                return _definitions.AllowCaching;
            }
        }
        private readonly ByteSerializer _serializer;
        private readonly string _defSeperator;
        private int _holdUpdateFlag = 0;
        public string name { get; }
        public bool SupportMultiAccess => (_definitions.share != FileShare.None);
        public PermaLabeledDictionary(string name, string defSeperator = null, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate) : this(new DotNetSerializer(), name, defSeperator, deleteOnDispose, access, share, mode) { }
        public PermaLabeledDictionary(ByteSerializer serializer ,string name, string defSeperator = null, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate)
        {
            _definitions = new PermaObject<string>(name, deleteOnDispose, access, share, mode, "");
            DeleteOnDispose = deleteOnDispose;
            _serializer = serializer;
            this.name = name;
            _defSeperator = defSeperator ?? Environment.NewLine;
            this.RefreshDefinitions(true);
        }
        private void RefreshDefinitions(bool overridemulti = false)
        {
            if ((SupportMultiAccess || overridemulti) && (_holdUpdateFlag == 0))
            {
                this._definitions.tryParse(out Exception ex);
                if (ex != null)
                    this._definitions.value = "";
                var defstring = this._definitions.value;
                this._dic = new Dictionary<string, IPermaObject<T>>(defstring.Count(_defSeperator));
                var keys = (defstring == ""
                    ? System.Linq.Enumerable.Empty<string>() : defstring.Split(new[] { _defSeperator }, StringSplitOptions.None));
                foreach (string s in keys.Take(Math.Max(0, keys.Count() - 1)))
                {
                    this._dic[s] = new PermaObject<T>(_serializer,
                        MutateFileName(name, k => "__DICTIONARYMEMBER_" + s + "_" + k), DeleteOnDispose, _definitions.access, _definitions.share, FileMode.Open);
                }
            }
        }
        public void MutauteValue(string i, Func<T, T> mutation)
        {
            _dic[i].MutauteValue(mutation);
        }
        public T tryParse(string i, out Exception ex)
        {
            this.RefreshDefinitions();
            return _dic[i].tryParse(out ex);
        }
        public bool ContainsKey(string key)
        {
            this.RefreshDefinitions();
            return _dic.ContainsKey(key);
        }
        public void Add(string key, T value)
        {
            this.RefreshDefinitions();
            _holdUpdateFlag++;
            this[key] = value;
            _holdUpdateFlag--;
        }
        public bool Remove(string key)
        {
            this.RefreshDefinitions();
            if (!_dic.ContainsKey(key))
                return false;
            StringBuilder newdef = new StringBuilder(_definitions.value.Length + Environment.NewLine.Length * 2);
            foreach (string s in _definitions.value.Split(new[] { _defSeperator }, StringSplitOptions.None))
            {
                if (s.Equals(key))
                    continue;
                newdef.Append(s + _defSeperator);
            }
            _definitions.value = newdef.ToString();
            _dic[key].Dispose();
            if (!DeleteOnDispose)
                File.Delete(_dic[key].name);
            _dic.Remove(key);
            return true;
        }
        public bool TryGetValue(string key, out T value)
        {
            this.RefreshDefinitions();
            value = default(T);
            if (!ContainsKey(key))
                return false;
            value = tryParse(key, out Exception e);
            return e == null;
        }
        public T this[string identifier]
        {
            get
            {
                this.RefreshDefinitions();
                return _dic[identifier].value;
            }
            set
            {
                this.RefreshDefinitions();
                if (identifier.Contains(_defSeperator))
                    throw new Exception("cannot create entry with the separator in it");
                if (!_dic.ContainsKey(identifier))
                {
                    _dic[identifier] = new PermaObject<T>(_serializer,
                        MutateFileName(name, k => "__DICTIONARYMEMBER_" + identifier + "_" + k), DeleteOnDispose, _definitions.access, _definitions.share, FileMode.Create);
                    _definitions.value += identifier + _defSeperator;
                }
                _dic[identifier].value = value;
            }
        }
        public ICollection<string> Keys
        {
            get
            {
                this.RefreshDefinitions();
                return _dic.Keys;
            }
        }
        public ICollection<T> Values
        {
            get
            {
                this.RefreshDefinitions();
                return _dic.Values.Select(a => a.value).ToArray();
            }
        }
        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            this.RefreshDefinitions();
            return _dic.Select(a => new KeyValuePair<string, T>(a.Key, a.Value.value)).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            this.RefreshDefinitions();
            return this.GetEnumerator();
        }
        public void Add(KeyValuePair<string, T> item)
        {
            this.RefreshDefinitions();
            _holdUpdateFlag++;
            Add(item.Key, item.Value);
            _holdUpdateFlag--;
        }
        public void Clear()
        {
            this.RefreshDefinitions();
            _holdUpdateFlag++;
            foreach (string key in Keys)
            {
                this.Remove(key);
            }
            _holdUpdateFlag--;
        }
        public bool Contains(KeyValuePair<string, T> item)
        {
            this.RefreshDefinitions();
            _holdUpdateFlag++;
            var contains = ContainsKey(item.Key) && this[item.Key].Equals(item.Value);
            _holdUpdateFlag--;
            return contains;
        }
        public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
        {
            this.RefreshDefinitions();
            _holdUpdateFlag++;
            foreach (var key in Keys.CountBind(arrayIndex))
            {
                array[key.Item2] = new KeyValuePair<string, T>(key.Item1, this[key.Item1]);
            }
            _holdUpdateFlag--;
        }
        public bool Remove(KeyValuePair<string, T> item)
        {
            this.RefreshDefinitions();
            _holdUpdateFlag++;
            var t = Contains(item);
            if (t)
            {
                Remove(item.Key);
                _holdUpdateFlag--;
                return true;
            }
            _holdUpdateFlag--;
            return false;
        }
        public int Count
        {
            get
            {
                this.RefreshDefinitions();
                return _dic.Count;
            }
        }
        public bool IsReadOnly
        {
            get
            {
                return !_definitions.access.HasFlag(FileAccess.Write);
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            this.RefreshDefinitions();
            if (disposing)
            {
                foreach (KeyValuePair<string, IPermaObject<T>> iPermaObject in _dic)
                {
                    iPermaObject.Value.DeleteOnDispose = DeleteOnDispose;
                    iPermaObject.Value.Dispose();
                }
                _definitions.DeleteOnDispose = DeleteOnDispose;
                _definitions.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
