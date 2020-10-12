using System;
using System.IO;
using Mjcheetham.Git.IndexViewer;
using Xunit;
using Index = Mjcheetham.Git.IndexViewer.Index;

namespace GitIndex.Tests
{
    public class IndexSerializerTests
    {
        [Fact]
        public void Deserialize()
        {
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string filePath = Path.Combine(
                homePath,
                "repos",
                "mjcheetham",
                "git",
                ".git",
                "index");

            byte[] inputBytes = File.ReadAllBytes(filePath);
            using var inputStream = new MemoryStream(inputBytes);
            Index index = IndexSerializer.Deserialize(inputStream);

            using var outputStream = new MemoryStream();
            IndexSerializer.Serialize(index, outputStream);
            byte[] outputBytes = outputStream.ToArray();

            Assert.Equal(inputBytes, outputBytes);
        }
    }
}
