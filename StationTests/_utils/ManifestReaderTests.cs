using System;
using Xunit;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._utils;

namespace StationTests._utils;

public class ManifestReaderTests
{
    private ManifestReader.ManifestApplicationList SetupOneApp()
    {
        ManifestReader.CreateVrManifestFile("./manifest_sample.vrmanifest", "tests");
        ManifestReader.CreateOrUpdateApplicationEntry("./manifest_sample.vrmanifest", "App", new JObject
        {
            { "id", "MyApp" },
            { "altPath", "MyApp" },
            { "name", "MyApp" },
            { "image_path", "./image.png" },
        });
        
        ManifestReader.ManifestApplicationList manifestApplicationList =
            new ManifestReader.ManifestApplicationList("./manifest_sample.vrmanifest");
        return manifestApplicationList;
    }

    private ManifestReader.ManifestApplicationList SetupTwoApps()
    {
        ManifestReader.CreateVrManifestFile("./manifest_sample.vrmanifest", "tests");
        ManifestReader.CreateOrUpdateApplicationEntry("./manifest_sample.vrmanifest", "App", new JObject
        {
            { "id", "MyApp" },
            { "altPath", "MyApp" },
            { "name", "MyApp" },
            { "image_path", "./image.png" },
        });
        ManifestReader.CreateOrUpdateApplicationEntry("./manifest_sample.vrmanifest", "App", new JObject
        {
            { "id", "App2" },
            { "altPath", "App2Path" },
            { "name", "AnotherApp" },
            { "image_path", "./image.png" },
            { "binary_path_windows", "./bin" },
        });
        ManifestReader.ManifestApplicationList manifestApplicationList =
            new ManifestReader.ManifestApplicationList("./manifest_sample.vrmanifest");
        return manifestApplicationList;
    }
    
    
    [Fact]
    public void CanCheckIfApplicationIsInstalled()
    {
        var manifestApplicationList = SetupOneApp();
        
        Assert.True(manifestApplicationList.IsApplicationInstalledAndVrCompatible("App.app.MyApp"));
    }
    
    [Fact]
    public void CanCheckIfApplicationIsNotInstalled()
    {
        
        var manifestApplicationList = SetupOneApp();
        
        Assert.False(manifestApplicationList.IsApplicationInstalledAndVrCompatible("App.app.SomeoneElsesApp"));
    }
    
    [Fact]
    public void CollectKeyAndNameReturnsKeyAndNameForInstalledApps()
    {
        SetupTwoApps();
        
        var result = ManifestReader.CollectKeyAndName("./manifest_sample.vrmanifest");
        Assert.Collection(result, 
            e => Assert.Equal("App.app.MyApp", e.Item1),
            e => Assert.Equal("App.app.App2", e.Item1)
        );
        Assert.Collection(result,
            e => Assert.Equal("MyApp", e.Item2),
            e => Assert.Equal("AnotherApp", e.Item2)
        );
    }
    
    [Fact]
    public void CanGetApplicationNameByKey()
    {
        SetupTwoApps();
        
        Assert.Equal(ManifestReader.GetApplicationNameByAppKey("./manifest_sample.vrmanifest", "App.app.App2"), "AnotherApp");
    }
    
    [Fact]
    public void GettingANonExistingAppNameReturnsNull()
    {
        SetupTwoApps();
        
        Assert.Equal(ManifestReader.GetApplicationNameByAppKey("./manifest_sample.vrmanifest", "App.app.App3"), null);
    }
    
    [Fact]
    public void CanGetApplicationImagePathByKey()
    {
        SetupTwoApps();
        
        Assert.Equal(ManifestReader.GetApplicationImagePathByAppKey("./manifest_sample.vrmanifest", "App.app.App2"), "./image.png");
    }
    
    [Fact]
    public void GettingANonExistingAppImagePathReturnsNull()
    {
        SetupTwoApps();
        
        Assert.Equal(ManifestReader.GetApplicationImagePathByAppKey("./manifest_sample.vrmanifest", "App.app.App3"), null);
    }
    
    [Fact]
    public void ModifyBinaryPathWillUpdateTheBinaryPathOfApps()
    {
        SetupTwoApps();
        
        ManifestReader.ModifyBinaryPath("./manifest_sample.vrmanifest", "./bin2");

        ManifestReader.ManifestApplicationList manifestApplicationList =
            new ManifestReader.ManifestApplicationList("./manifest_sample.vrmanifest");
        Assert.Equal("./bin2/MyApp", ((JObject) manifestApplicationList.GetApplication("App.app.MyApp"))["binary_path_windows"]);
        Assert.Equal("./bin2/App2Path", ((JObject) manifestApplicationList.GetApplication("App.app.App2"))["binary_path_windows"]);
    }
    
    [Fact]
    public void ModifyBinaryPathWillNotFailIfFileDoesNotExist()
    {
        SetupTwoApps();
        
        ManifestReader.ModifyBinaryPath("./manifest_sample_not_real.vrmanifest", "./bin2");
    }
    
    [Fact]
    public void CanEmptyOutManifestFileWithClearApplicationList()
    {
        SetupTwoApps();
        
        ManifestReader.ClearApplicationList("./manifest_sample.vrmanifest");
        ManifestReader.ManifestApplicationList manifestApplicationList =
            new ManifestReader.ManifestApplicationList("./manifest_sample.vrmanifest");
        Assert.False(manifestApplicationList.IsApplicationInstalledAndVrCompatible("App.app.MyApp"));
        Assert.False(manifestApplicationList.IsApplicationInstalledAndVrCompatible("App.app.App2"));
    }
    
    [Fact]
    public void CanUpdateDetailsOnApp()
    {
        SetupTwoApps();
        
        ManifestReader.CreateOrUpdateApplicationEntry("./manifest_sample.vrmanifest", "App", new JObject
        {
            { "id", "App2" },
            { "name", "AnotherApp2" }
        });
        
        Assert.Equal("AnotherApp2", ManifestReader.GetApplicationNameByAppKey("./manifest_sample.vrmanifest", "App.app.App2"));
    }
}
