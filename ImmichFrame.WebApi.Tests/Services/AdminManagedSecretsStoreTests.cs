using ImmichFrame.WebApi.Services;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Services;

[TestFixture]
public class AdminManagedSecretsStoreTests
{
    [Test]
    public void Load_WhenSecretsJsonIsMalformed_ReturnsEmptyDocument()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var storePath = Path.Combine(tempDirectory, "admin-secrets.json");

        try
        {
            File.WriteAllText(storePath, "{ this is not valid json");

            var store = new AdminManagedSecretsStore(new AdminManagedSecretsStoreOptions
            {
                StorePath = storePath
            });

            var secrets = store.Load();

            Assert.That(secrets.WeatherApiKey, Is.Null.Or.Empty);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
