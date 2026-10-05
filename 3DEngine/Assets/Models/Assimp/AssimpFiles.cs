using A = Assimp;

namespace Engine;

/// <summary>
/// The files Assimp reads for one import, each opened as a C# stream from wherever the model came
/// from, so Assimp opens no file of its own.
/// </summary>
/// <remarks>
/// <para>
/// Left to itself, Assimp opens a model and the files beside it through the C runtime, whose
/// handles a child process inherits on Windows when it starts while one is open, and keeps until
/// it exits, so a file read in one thread could stay held by a compiler started in another. A
/// <see cref="FileStream"/> is opened uninheritable, so nothing read here leaves the process.
/// </para>
/// <para>
/// The opener is given each name as Assimp asks for it, with forward slashes, which for the files
/// beside a model is the model's own name with the file name changed, as <c>models/tri.mtl</c>
/// for <c>models/tri.obj</c>. A model read from an asset reader therefore finds its
/// <c>.mtl</c> and <c>.bin</c> in that reader, an embedded or an in-memory one as well as a
/// folder.
/// </para>
/// <para>
/// Each method here is called from native code, where an exception would end the process, and
/// what it calls is a reader, which a game may write and which may throw anything. So every
/// exception is caught, a file that could not be opened or read is reported to Assimp as missing
/// or short, and the first is kept, for <see cref="ThrowIfFailed"/> to answer the load with once
/// Assimp has returned.
/// </para>
/// </remarks>
internal sealed class AssimpFiles(Func<string, Stream?> open) : A.IOSystem
{
    private Exception? _failure;
    private string? _failed;

    /// <summary>The name whose every open reads <see cref="Shared"/>, each from a place of its own in it, as the model itself.</summary>
    public string? SharedName { get; init; }

    /// <summary>The seekable stream <see cref="SharedName"/> opens, which the caller keeps and disposes.</summary>
    public Stream? Shared { get; init; }

    /// <inheritdoc />
    public override A.IOStream? OpenFile(string pathToFile, A.FileIOMode fileMode)
    {
        if (fileMode is not (A.FileIOMode.Read or A.FileIOMode.ReadBinary or A.FileIOMode.ReadText))
            return null;
        var name = pathToFile.Replace('\\', '/');
        try
        {
            while (name.StartsWith("./", StringComparison.Ordinal)) name = name[2..];
            if (Shared is not null && name == SharedName) return new File(this, pathToFile, fileMode, Shared, owned: false);
            var stream = open(name);
            return stream is null ? null : new File(this, pathToFile, fileMode, stream, owned: true);
        }
        catch (Exception ex)
        {
            Fail(name, ex);
            return null;
        }
    }

    /// <summary>
    /// Throws an <see cref="IOException"/> naming the first file whose opening or reading failed,
    /// with the reader's exception inside it, where one did.
    /// </summary>
    public void ThrowIfFailed()
    {
        if (_failure is not null)
            throw new IOException($"'{_failed}' could not be read: {_failure.Message}", _failure);
    }

    private void Fail(string name, Exception ex)
    {
        if (_failure is not null) return;
        (_failure, _failed) = (ex, name);
    }

    /// <summary>A file on disk opened by name from a folder, or null where there is none.</summary>
    public static Stream? OnDisk(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        return System.IO.File.Exists(path) ? System.IO.File.OpenRead(path) : null;
    }

    // One open of a file, at a place of its own in the stream, so two opens of a stream shared
    // between them do not move each other's place. Assimp's reads land in its own memory straight
    // from the stream, where the binding's read reads into an array as long as the read and copies
    // it over, which for a model read whole is the file once more on the managed heap.
    private sealed unsafe class File : A.IOStream
    {
        private readonly AssimpFiles _files;
        private readonly bool _owned;
        private Stream? _stream;
        private long _position;

        public File(AssimpFiles files, string path, A.FileIOMode mode, Stream stream, bool owned) : base(path, mode, initialize: false)
        {
            (_files, _stream, _owned) = (files, stream, owned);
            Initialize(OnAiFileWriteProc, ReadInto, OnAiFileTellProc, OnAiFileSizeProc, OnAiFileSeekProc, OnAiFileFlushProc, IntPtr.Zero);
        }

        // fread's contract, the whole elements read. The callbacks are this open's own, so the
        // file Assimp names is always this one.
        private UIntPtr ReadInto(IntPtr file, IntPtr data, UIntPtr elementSize, UIntPtr elements)
        {
            var size = elementSize.ToUInt64();
            if (size == 0) return UIntPtr.Zero;
            var wanted = (int)Math.Min(size * elements.ToUInt64(), int.MaxValue);
            return (UIntPtr)((ulong)ReadInto(new Span<byte>((void*)data, wanted)) / size);
        }

        private int ReadInto(Span<byte> into)
        {
            if (_stream is null) return 0;
            try
            {
                _stream.Position = _position;
                var read = _stream.ReadAtLeast(into, into.Length, throwOnEndOfStream: false);
                _position += read;
                return read;
            }
            catch (Exception ex)
            {
                _files.Fail(PathToFile, ex);
                return 0;
            }
        }

        public override bool IsValid => _stream is not null;

        public override long Write(byte[] dataToWrite, long count) => 0;

        // Assimp reads through ReadInto, and this is here because the binding declares it.
        public override long Read(byte[] dataRead, long count) => ReadInto(dataRead.AsSpan(0, (int)Math.Min(count, dataRead.Length)));

        public override A.ReturnCode Seek(long offset, A.Origin seekOrigin)
        {
            if (_stream is null) return A.ReturnCode.Failure;
            try
            {
                var to = seekOrigin switch
                {
                    A.Origin.Set => offset,
                    A.Origin.Current => _position + offset,
                    _ => _stream.Length + offset,
                };
                if (to < 0) return A.ReturnCode.Failure;
                _position = to;
                return A.ReturnCode.Success;
            }
            catch (Exception ex)
            {
                _files.Fail(PathToFile, ex);
                return A.ReturnCode.Failure;
            }
        }

        public override long GetPosition() => _position;

        public override long GetFileSize()
        {
            try
            {
                return _stream?.Length ?? 0;
            }
            catch (Exception ex)
            {
                _files.Fail(PathToFile, ex);
                return 0;
            }
        }

        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _owned)
            {
                try
                {
                    _stream?.Dispose();
                }
                catch (Exception ex)
                {
                    _files.Fail(PathToFile, ex);
                }
            }
            _stream = null;
            base.Dispose(disposing);
        }
    }
}
