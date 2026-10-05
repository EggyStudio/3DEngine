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
/// Each method here is called from native code, where an exception would end the process, so a
/// file that cannot be opened or read is reported to Assimp as missing or short, and Assimp says
/// what it lacked.
/// </para>
/// </remarks>
internal sealed class AssimpFiles(Func<string, Stream?> open) : A.IOSystem
{
    /// <inheritdoc />
    public override A.IOStream? OpenFile(string pathToFile, A.FileIOMode fileMode)
    {
        if (fileMode is not (A.FileIOMode.Read or A.FileIOMode.ReadBinary or A.FileIOMode.ReadText))
            return null;
        try
        {
            var name = pathToFile.Replace('\\', '/');
            while (name.StartsWith("./", StringComparison.Ordinal)) name = name[2..];
            var stream = open(name);
            return stream is null ? null : new File(pathToFile, fileMode, stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A file on disk opened by name from a folder, or null where there is none.</summary>
    public static Stream? OnDisk(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        return System.IO.File.Exists(path) ? System.IO.File.OpenRead(path) : null;
    }

    private sealed class File(string path, A.FileIOMode mode, Stream stream) : A.IOStream(path, mode)
    {
        private Stream? _stream = stream;

        public override bool IsValid => _stream is not null;

        public override long Write(byte[] dataToWrite, long count) => 0;

        public override long Read(byte[] dataRead, long count)
        {
            if (_stream is null) return 0;
            try
            {
                return _stream.ReadAtLeast(dataRead.AsSpan(0, (int)count), (int)count, throwOnEndOfStream: false);
            }
            catch (IOException)
            {
                return 0;
            }
        }

        public override A.ReturnCode Seek(long offset, A.Origin seekOrigin)
        {
            if (_stream is null) return A.ReturnCode.Failure;
            try
            {
                _stream.Seek(offset, seekOrigin switch
                {
                    A.Origin.Set => SeekOrigin.Begin,
                    A.Origin.Current => SeekOrigin.Current,
                    _ => SeekOrigin.End,
                });
                return A.ReturnCode.Success;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
            {
                return A.ReturnCode.Failure;
            }
        }

        public override long GetPosition() => _stream?.Position ?? 0;

        public override long GetFileSize() => _stream?.Length ?? 0;

        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream?.Dispose();
                _stream = null;
            }
            base.Dispose(disposing);
        }
    }
}
