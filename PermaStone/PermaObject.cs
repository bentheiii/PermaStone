using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CipherStone;
using WhetStone.Looping;
using WhetStone.Streams;
using WhetStone.Units.Time;
using WhetStone.WordPlay;
using static PermaStone.Utility;

namespace PermaStone
{
    //todo disable sharing, rework deleteondispose
    public interface ISafeDeletable : IDisposable
    {
        bool DeleteOnDispose { get; set; }
        FileAccess access { get; }
        FileShare share { get; }
        bool AllowCaching { get; }
    }
    public abstract class IPermaObject<T> : ISafeDeletable
    {
        public abstract T tryParse(out Exception ex);
        public abstract T value { get; set; }
        public abstract string name { get; }
        public abstract bool DeleteOnDispose { get; set; }
        public abstract FileAccess access { get; }
        public abstract FileShare share { get; }
        public abstract bool AllowCaching { get; }
        public abstract void Dispose();
        public void MutauteValue(Func<T, T> mutation)
        {
            this.value = mutation(this.value);
        }
        public void MutauteValue(Action<T> mutation)
        {
            var v = this.value;
            mutation(v);
            this.value = v;
        }
    }
    public static class PermaObject
    {

        public static string LocalName<T>(this IPermaObject<T> @this)
        {
            var s = @this.name;
            return Path.GetFileName(s);
        }
        public static bool Readable<T>(this IPermaObject<T> @this)
        {
            var temp = @this.tryParse(out Exception ex);
            return ex == null;
        }
        public static TimeSpan timeSinceUpdate<T>(this ISyncPermaObject<T> @this)
        {
            return DateTime.Now.Subtract(@this.getLatestUpdateTime());
        }
    }
    public class PermaObject<T> : IPermaObject<T>
    {
        private readonly FileStream _stream;
        public override FileAccess access { get; }
        public sealed override FileShare share { get; }
        public override bool AllowCaching { get; }
        public sealed override bool DeleteOnDispose { get; set; }
        private readonly ByteSerializer _serializer;
        private Tuple<T, bool> _cache;
        public PermaObject(string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, T valueIfCreated = default(T), bool allowCaching = true) :
            this(new DotNetSerializer(), name, deleteOnDispose, access, share, mode, valueIfCreated, allowCaching) {}
        public PermaObject(ByteSerializer serializer, string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, T valueIfCreated = default(T), bool allowCaching = true)
        {
            name = Path.GetFullPath(name);
            if (mode == FileMode.Truncate || mode == FileMode.Append)
                throw new ArgumentException("truncate and append modes are not supported", nameof(mode));
            bool create = !File.Exists(name);
            if (mode != FileMode.Open && valueIfCreated == null)
                throw new ArgumentException("is the default value is null, the PermaObject cannot be newly created");
            if (deleteOnDispose && (!create && share != FileShare.None))
                throw new ArgumentException("delete on dispose demands the file not previously exist or that sharing will be none", nameof(deleteOnDispose));
            this._serializer = serializer;
            this.access = access;
            this.share = share;
            this.AllowCaching = allowCaching;
            FileOptions options = FileOptions.SequentialScan;
            if (share != FileShare.None)
                options |= FileOptions.Asynchronous;
            DeleteOnDispose = deleteOnDispose;
            Directory.CreateDirectory(Path.GetDirectoryName(name));
            _stream = new FileStream(name, mode, access, share, 4096, options);
            if (create)
                this.value = valueIfCreated;
            _cache = (this.share == FileShare.None && allowCaching) ? Tuple.Create(default(T), false) : null;
        }
        public override T tryParse(out Exception ex)
        {
            ex = null;
            if (!access.HasFlag(FileAccess.Read))
                throw new AccessViolationException("permaobject is set not to read");
            if (_cache?.Item2 ?? false)
                return _cache.Item1;
            try
            {
                _stream.Seek(0, SeekOrigin.Begin);
                var b = _stream.ReadAll();
                var ret = _serializer.deserialize<T>(b);
                if (_cache != null)
                    _cache = Tuple.Create(ret, true);
                return ret;
            }
            catch (Exception e)
            {
                ex = e;
                return default(T);
            }
        }
        public sealed override T value
        {
            get
            {
                T ret = tryParse(out Exception prox);
                if (prox != null)
                    throw prox;
                return ret;
            }
            set
            {
                if (!access.HasFlag(FileAccess.Write))
                    throw new AccessViolationException("permaobject is set not to write");
                byte[] buffer = _serializer.serialize(value);
                _stream.Seek(0, SeekOrigin.Begin);
                _stream.SetLength(0);
                _stream.Write(buffer, 0, buffer.Length);
                _stream.Flush(true);
                if (_cache != null)
                    _cache = Tuple.Create(value, true);
            }
        }
        public override string name
        {
            get
            {
                return _stream.Name;
            }
        }
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                (this._stream as IDisposable).Dispose();
                if (DeleteOnDispose)
                    File.Delete(this.name);
            }
        }
    }
    public abstract class ISyncPermaObject<T> : IPermaObject<T>
    {
        public abstract T getFresh(DateTime earliestTime);
        public abstract T getFresh(TimeSpan maxInterval);
        public abstract DateTime getLatestUpdateTime();
    }
    public class SyncPermaObject<T> : ISyncPermaObject<T>
    {
        internal const string PERMA_OBJ_UPDATE_EXTENSION = ".permaobjupdate";
        private readonly PermaObject<T> _int;
        private readonly PermaObject<DateTime> _update;
        public SyncPermaObject(string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, T valueIfCreated = default(T), bool allowCaching = true) :
            this(new DotNetSerializer(), name, deleteOnDispose, access, share, mode, valueIfCreated, allowCaching) { }
        public SyncPermaObject(ByteSerializer serializer, string name, bool deleteOnDispose = false, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.None, FileMode mode = FileMode.OpenOrCreate, T valueIfCreated = default(T), bool allowCaching = true)
        {
            _int = new PermaObject<T>(serializer, name, deleteOnDispose, access, share, mode, valueIfCreated, allowCaching);
            _update = new PermaObject<DateTime>(MutateFileName(name, a => "__LATESTUPDATE_" + a), deleteOnDispose, access, share, mode, DateTime.Now, allowCaching);
        }
        public override T getFresh(TimeSpan maxInterval)
        {
            return getFresh(maxInterval, maxInterval.Divide(2));
        }
        public T getFresh(TimeSpan maxInterval, TimeSpan checkinterval)
        {
            while (this.timeSinceUpdate() > maxInterval)
            {
                Thread.Sleep(checkinterval);
            }
            return this.value;
        }
        public override T getFresh(DateTime earliestTime)
        {
            return getFresh(earliestTime, TimeSpan.FromSeconds(0.5));
        }
        public T getFresh(DateTime earliestTime, TimeSpan checkinterval)
        {
            while (getLatestUpdateTime() < earliestTime)
            {
                Thread.Sleep(checkinterval);
            }
            return this.value;
        }
        public override DateTime getLatestUpdateTime()
        {
            var a = _update.tryParse(out Exception e);
            return (e == null) ? a : DateTime.MinValue;
        }
        public override T tryParse(out Exception ex)
        {
            return _int.tryParse(out ex);
        }
        public override T value
        {
            get
            {
                return _int.value;
            }
            set
            {
                _update.value = DateTime.Now;
                _int.value = value;
            }
        }
        public override string name
        {
            get
            {
                return _int.name;
            }
        }
        public override FileAccess access
        {
            get
            {
                return _int.access;
            }
        }
        public override FileShare share
        {
            get
            {
                return _int.share;
            }
        }
        public override bool AllowCaching
        {
            get
            {
                return _int.AllowCaching;
            }
        }
        public override bool DeleteOnDispose
        {
            get
            {
                return _int.DeleteOnDispose;
            }
            set
            {
                _int.DeleteOnDispose = value;
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _int.Dispose();
                _update.Dispose();
            }
        }
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
