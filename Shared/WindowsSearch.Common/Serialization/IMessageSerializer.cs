namespace WindowsSearch.Common.Serialization;

public interface IMessageSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T value);
    T Deserialize<T>(byte[] data);
    string FormatForLog<T>(T value);
}
