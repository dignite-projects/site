using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

public class SiteViewLocalizer_Tests
{
    private readonly RecordingHtmlLocalizerFactory _factory = new();

    private SiteViewLocalizer Contextualized(string executingFilePath)
    {
        var localizer = new SiteViewLocalizer(
            _factory, new TestWebHostEnvironment(), new NamedHtmlLocalizer<SiteTemplateResource>("template"));
        localizer.Contextualize(new ViewContext { ExecutingFilePath = executingFilePath });
        return localizer;
    }

    [Theory]
    [InlineData("/Sites/Blog/Index.cshtml")]
    [InlineData("/Sites/acme/Blog/Index.cshtml")]
    [InlineData("/sites/Shared/_SiteLayout.cshtml")]
    public void Should_Read_The_Template_Resource_Inside_Sites(string path)
    {
        var localizer = Contextualized(path);

        localizer["Greeting"].Value.ShouldBe("template:Greeting");
        _factory.BaseNames.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/Views/Home/Index.cshtml", "TestApp.Views.Home.Index")]
    [InlineData("/SitesArchive/Index.cshtml", "TestApp.SitesArchive.Index")]
    public void Should_Behave_Like_The_Default_ViewLocalizer_Elsewhere(string path, string expectedBaseName)
    {
        var localizer = Contextualized(path);

        localizer["Greeting"].Value.ShouldBe($"{expectedBaseName}:Greeting");
        _factory.BaseNames.ShouldBe(new[] { expectedBaseName });
    }

    [Fact]
    public void Should_Pass_Format_Arguments_Through()
    {
        var localizer = Contextualized("/Sites/Blog/Index.cshtml");

        localizer.GetString("Greeting", "Ada").Value.ShouldBe("template:Greeting(Ada)");
    }

    private class RecordingHtmlLocalizerFactory : IHtmlLocalizerFactory
    {
        public List<string> BaseNames { get; } = new();

        public IHtmlLocalizer Create(Type resourceSource) => throw new NotSupportedException();

        public IHtmlLocalizer Create(string baseName, string location)
        {
            BaseNames.Add(baseName);
            return new NamedHtmlLocalizer<object>(baseName);
        }
    }

    private class NamedHtmlLocalizer<T> : IHtmlLocalizer<T>
    {
        private readonly string _source;

        public NamedHtmlLocalizer(string source) => _source = source;

        public LocalizedHtmlString this[string name] => new(name, $"{_source}:{name}");

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, $"{_source}:{name}", false, arguments);

        public LocalizedString GetString(string name) => new(name, $"{_source}:{name}");

        public LocalizedString GetString(string name, params object[] arguments) =>
            new(name, $"{_source}:{name}({string.Join(",", arguments)})");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TestApp";
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = default!;
        public IFileProvider WebRootFileProvider { get; set; } = default!;
        public string ContentRootPath { get; set; } = default!;
        public IFileProvider ContentRootFileProvider { get; set; } = default!;
    }
}
