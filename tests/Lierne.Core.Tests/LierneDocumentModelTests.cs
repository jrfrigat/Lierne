namespace Lierne.Core.Tests;

/// <summary>Tests document reuse, dirty state, view selection and view state.</summary>
public sealed class LierneDocumentModelTests
{
    private static readonly DocumentTypeId Type = new("tests.document");
    private static readonly DocumentViewId Summary = new("tests.summary");
    private static readonly DocumentViewId Details = new("tests.details");

    [Fact]
    public void ReusesKeyedDocumentAndUpdatesPayload()
    {
        var documents = CreateDocuments();

        var first = documents.Open(Type, key: new DocumentKey("tests:one"), payload: "one");
        var second = documents.Open(
            Type,
            title: TestDescriptors.Key("renamed"),
            payload: "two",
            key: new DocumentKey("tests:one"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(documents.Documents);
        Assert.Equal("two", second.Data.Payload);
        Assert.Equal(TestDescriptors.Key("renamed"), second.Data.Title);
        Assert.True(second.Data.Dirty);
        Assert.Equal(1, second.Data.Version);

        var third = documents.Open(
            Type,
            key: new DocumentKey("tests:one"),
            reusePolicy: DocumentReusePolicy.AlwaysNew);

        Assert.True(third.Success);
        Assert.NotEqual(first.Data.Id, third.Data!.Id);
        Assert.Equal(2, documents.Documents.Count);
    }

    [Fact]
    public void DirtyStateTracksVersionAndSavedContent()
    {
        var documents = CreateDocuments();
        var document = documents.Open(Type).Data!;

        Assert.False(document.Dirty);
        Assert.Equal(0, document.Version);

        Assert.True(documents.SetPayload(document.Id, "first").Success);
        var edited = documents.Documents.Single();
        Assert.True(edited.Dirty);
        Assert.Equal(1, edited.Version);

        Assert.True(documents.MarkSaved(document.Id, 1).Success);
        Assert.False(documents.Documents.Single().Dirty);

        Assert.True(documents.SetPayload(
            document.Id, new string("second".ToCharArray())).Success);
        var editedAgain = documents.Documents.Single();
        Assert.True(editedAgain.Dirty);
        Assert.Equal(2, editedAgain.Version);

        Assert.True(documents.MarkSaved(document.Id, 1).Success);
        Assert.True(documents.Documents.Single().Dirty);
    }

    [Fact]
    public void SelectedViewPreviewAndPinnedAreControlled()
    {
        var documents = CreateDocuments();
        var document = documents.Open(Type).Data!;

        Assert.Equal(Summary, document.SelectedView);
        Assert.True(documents.SetSelectedView(document.Id, Details).Success);
        Assert.Equal(Details, documents.Documents.Single().SelectedView);

        var invalid = documents.SetSelectedView(document.Id, new DocumentViewId("tests.missing"));
        Assert.False(invalid.Success);
        Assert.Equal("lattice.document.unknown_view", invalid.ErrorCode);

        Assert.True(documents.SetPreview(document.Id, true).Success);
        Assert.True(documents.SetPinned(document.Id, true).Success);
        var updated = documents.Documents.Single();
        Assert.True(updated.Preview);
        Assert.True(updated.Pinned);
    }

    [Fact]
    public void ViewStateIsStoredAndRemoved()
    {
        var documents = CreateDocuments();
        var document = documents.Open(Type).Data!;

        Assert.True(documents.SetViewState(document.Id, Details, "scroll:10").Success);
        Assert.Equal("scroll:10", documents.Documents.Single().ViewStates![Details]);

        Assert.True(documents.SetViewState(document.Id, Details, null).Success);
        Assert.Null(documents.Documents.Single().ViewStates);

        var invalid = documents.SetViewState(document.Id, new DocumentViewId("tests.missing"), "state");
        Assert.False(invalid.Success);
        Assert.Equal("lattice.document.unknown_view", invalid.ErrorCode);
    }

    [Fact]
    public void RepeatedDocumentOperationsDoNotReplaceTheInstanceOrNotify()
    {
        var documents = CreateDocuments();
        var key = new DocumentKey("tests:one");
        var document = documents.Open(Type, key: key).Data!;
        var notifications = 0;
        documents.Changed += () => notifications++;

        Assert.True(documents.SetSelectedView(document.Id, Summary).Success);
        Assert.True(documents.SetViewState(document.Id, Details, null).Success);
        Assert.Same(document, documents.Open(Type, key: key, view: Summary).Data);
        Assert.Same(document, documents.Active);
        Assert.Equal(0, notifications);

        var second = documents.Open(Type, key: new("tests:two")).Data!;
        Assert.True(documents.Close(document.Id).Success);
        Assert.True(documents.SetSelectedView(second.Id, Details).Success);
        Assert.Equal(Details, documents.Active!.SelectedView);
    }

    private static LierneDocumentService CreateDocuments() =>
        new(BuildCatalog());

    private static LiernePluginCatalog BuildCatalog() =>
        LiernePluginCatalog.Build(
        [
            new TestPlugin("tests.documents", builder =>
            {
                builder.AddDocument(new DocumentDescriptor(
                    Type,
                    TestDescriptors.Key("document"),
                    "file",
                    typeof(object),
                    ReusePolicy: DocumentReusePolicy.ReuseByKey,
                    DefaultView: Summary));
                builder.AddDocumentView(new DocumentViewDescriptor(
                    Type, Summary, TestDescriptors.Key("summary"), null, typeof(object)));
                builder.AddDocumentView(new DocumentViewDescriptor(
                    Type, Details, TestDescriptors.Key("details"), null, typeof(object), Order: 10));
            })
        ]);
}
