using System.Text;

namespace TraducaoScbr.Core.Translation;

/// <summary>Um arquivo de texto lido com a codificação dele, para gravar de volta do mesmo jeito.</summary>
internal sealed record TextFile(string Content, Encoding Encoding, bool Bom)
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static TextFile Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.AsSpan().StartsWith(Utf8Bom);
        var body = bom ? bytes[3..] : bytes;
        try
        {
            var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return new TextFile(strict.GetString(body), strict, bom);
        }
        catch (DecoderFallbackException)
        {
            // Não é UTF-8: o Latin-1 lê e grava de volta todos os bytes sem trocar nenhum.
            return new TextFile(Encoding.Latin1.GetString(body), Encoding.Latin1, false);
        }
    }

    public byte[] ToBytes(string content)
    {
        var body = Encoding.GetBytes(content);
        return Bom ? [.. Utf8Bom, .. body] : body;
    }

    /// <summary>Grava pelo arquivo temporário ao lado e troca, para nunca deixar o arquivo pela metade.</summary>
    public static void WriteAtomic(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        var info = new FileInfo(path);
        var readOnly = info.Exists && info.IsReadOnly;
        if (readOnly)
        {
            info.IsReadOnly = false;
        }

        try
        {
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
        finally
        {
            if (readOnly)
            {
                new FileInfo(path).IsReadOnly = true;
            }
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
