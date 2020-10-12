using System;

namespace Mjcheetham.Git.IndexViewer
{
    public class ObjectId
    {
        public byte[] Bytes { get; }

        public ObjectId(byte[] bytes)
        {
            Bytes = bytes;
        }

        public string ToString(int len)
        {
            return ToString().Substring(0, len);
        }

        public override string ToString()
        {
            return BitConverter.ToString(Bytes)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        public ObjectIdType Type =>
            Bytes switch
            {
                {Length: Constants.Sha1Length} => ObjectIdType.Sha1,
                {Length: Constants.Sha256Length} => ObjectIdType.Sha256,
                _ => ObjectIdType.Unknown
            };
    }

    public enum ObjectIdType
    {
        Unknown,
        Sha1,
        Sha256,
    }
}
