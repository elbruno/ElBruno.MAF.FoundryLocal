using System.Reflection;
using System.Text.Json;
using ElBruno.MAF.FoundryLocal;
using Microsoft.AI.Foundry.Local;

namespace ElBruno.MAF.FoundryLocal.Tests;

public class FoundryLocalDecisionModelResolverTests
{
    private const string ModelPath = @"C:\models\kev";

    [Theory]
    [InlineData("kev-0.8b", "decision")]
    [InlineData("another-decision-model", "typed-decision")]
    public async Task Resolve_RegistersSelectedModelUsingItsExistingPath(string selectedAlias, string task)
    {
        var source = Model(task, "kev-cpu:2", ModelPath);
        var registered = Model("typed-decision", "elbruno-maf-foundrylocal-kev-cpu-typed-decision:2", ModelPath);
        string? lookedUpAlias = null;
        string? registeredPath = null;
        string? registeredId = null;
        var publicCatalog = Catalog((name, args) =>
        {
            if (name == "GetModelAsync" && Equals(args[0], selectedAlias))
            {
                lookedUpAlias = (string)args[0]!;
                return Task.FromResult<IModel?>(source);
            }

            return name switch
            {
                "GetModelAsync" or "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
                _ => throw new InvalidOperationException(name)
            };
        });
        var localCatalog = Catalog((name, args) => name switch
        {
            "GetModelAsync" or "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
            "RegisterModelAsync" => (object)Register(args),
            _ => throw new InvalidOperationException(name)
        });

        var result = await Resolve(publicCatalog, localCatalog, selectedAlias);

        Assert.Same(registered, result);
        Assert.Equal(selectedAlias, lookedUpAlias);
        Assert.Equal(ModelPath, registeredPath);
        Assert.Equal("elbruno-maf-foundrylocal-kev-cpu-typed-decision:2", registeredId);

        Task<IModel> Register(object?[] args)
        {
            registeredPath = (string)args[0]!;
            registeredId = (string)args[1]!;
            Assert.IsType<ModelInfoBuilder>(args[2]);
            return Task.FromResult(registered);
        }
    }

    [Fact]
    public async Task Resolve_ReusesMatchingLocalRegistration()
    {
        var source = Model("decision", "kev-cpu:2", ModelPath);
        var localId = "elbruno-maf-foundrylocal-kev-cpu-typed-decision:2";
        var existing = Model("typed-decision", localId, ModelPath);
        var publicCatalog = PublicCatalog(source, "kev-0.8b");
        var localCatalog = Catalog((name, args) =>
        {
            Assert.Equal("GetModelVariantAsync", name);
            Assert.Equal(localId, args[0]);
            return Task.FromResult<IModel?>(existing);
        });

        Assert.Same(existing, await Resolve(publicCatalog, localCatalog, "kev-0.8b"));
    }

    [Theory]
    [InlineData("chat", ModelPath, "already registered with task")]
    [InlineData("typed-decision", @"C:\models\other", "different model path")]
    public async Task Resolve_RejectsConflictingLocalRegistration(string task, string path, string message)
    {
        var source = Model("decision", "kev-cpu:2", ModelPath);
        var existing = Model(task, "elbruno-maf-foundrylocal-kev-cpu-typed-decision:2", path);
        var publicCatalog = PublicCatalog(source, "kev-0.8b");
        var localCatalog = Catalog((name, _) => name switch
        {
            "GetModelVariantAsync" => Task.FromResult<IModel?>(existing),
            "GetModelAsync" => Task.FromResult<IModel?>(existing),
            _ => throw new InvalidOperationException(name)
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolve(publicCatalog, localCatalog, "kev-0.8b"));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public async Task Resolve_RejectsPublicCatalogIdentityCollision()
    {
        var source = Model("decision", "kev-cpu:2", ModelPath);
        var collision = Model("chat", "elbruno-maf-foundrylocal-kev-cpu-typed-decision:1", ModelPath);
        var publicCatalog = Catalog((name, args) =>
        {
            if (name == "GetModelAsync" && Equals(args[0], "kev-0.8b"))
                return Task.FromResult<IModel?>(source);
            if (name == "GetModelAsync" &&
                Equals(args[0], "elbruno-maf-foundrylocal-kev-cpu-typed-decision"))
                return Task.FromResult<IModel?>(collision);
            return Task.FromResult<IModel?>(null);
        });
        var localCatalog = Catalog((name, _) => throw new InvalidOperationException(name));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolve(publicCatalog, localCatalog, "kev-0.8b"));
        Assert.Contains("public catalog", error.Message);
    }

    [Fact]
    public async Task Resolve_RejectsLocalAliasCollisionWithDifferentVersion()
    {
        var source = Model("decision", "kev-cpu:2", ModelPath);
        var collision = Model("typed-decision", "elbruno-maf-foundrylocal-kev-cpu-typed-decision:1", ModelPath);
        var publicCatalog = PublicCatalog(source, "kev-0.8b");
        var localCatalog = Catalog((name, _) => name switch
        {
            "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
            "GetModelAsync" => Task.FromResult<IModel?>(collision),
            _ => throw new InvalidOperationException(name)
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolve(publicCatalog, localCatalog, "kev-0.8b"));
        Assert.Contains("already registered locally", error.Message);
    }

    [Theory]
    [InlineData("{\"Name\":\"base:2\"}")]
    [InlineData("{\"Alias\":\"elbruno-maf-foundrylocal-base-typed-decision\"}")]
    public void EnsureCompatiblePackageAlias_AllowsUnclaimedOrMatchingIdentity(string json)
    {
        using var document = JsonDocument.Parse(json);
        FoundryLocalDecisionModelResolver.EnsureCompatiblePackageAlias(
            document.RootElement, "elbruno-maf-foundrylocal-base-typed-decision");
    }

    [Theory]
    [InlineData("{\"Alias\":\"kev-0.8b\"}")]
    [InlineData("{\"Alias\":42}")]
    [InlineData("[]")]
    public void EnsureCompatiblePackageAlias_RejectsPackageAliasOverride(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidOperationException>(() =>
            FoundryLocalDecisionModelResolver.EnsureCompatiblePackageAlias(
                document.RootElement, "elbruno-maf-foundrylocal-base-typed-decision"));
    }

    [Fact]
    public async Task Resolve_RejectsPackageAliasOverrideBeforeRegistering()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "inference_model.json"), "{\"Alias\":\"kev-0.8b\"}");
        try
        {
            var source = Model("decision", "kev-cpu:2", path);
            var publicCatalog = PublicCatalog(source, "kev-0.8b");
            var localCatalog = Catalog((name, _) => name switch
            {
                "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
                "GetModelAsync" => Task.FromResult<IModel?>(null),
                "RegisterModelAsync" => throw new Xunit.Sdk.XunitException("Must not register an aliased package."),
                _ => throw new InvalidOperationException(name)
            });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Resolve(publicCatalog, localCatalog, "kev-0.8b"));
            Assert.Contains("alias metadata", error.Message);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task Resolve_UsesExactVersionAndRecoversConcurrentRegistration()
    {
        var source = Model("decision", "kev-cpu:2", ModelPath);
        var registered = Model("typed-decision", "elbruno-maf-foundrylocal-kev-cpu-typed-decision:2", ModelPath);
        var registrationAttempted = false;
        var publicCatalog = PublicCatalog(source, "kev-0.8b");
        var localCatalog = Catalog((name, args) => name switch
        {
            "GetModelVariantAsync" when registrationAttempted => Task.FromResult<IModel?>(registered),
            "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
            "GetModelAsync" => Task.FromResult<IModel?>(null),
            "RegisterModelAsync" => (object)Register(args),
            _ => throw new InvalidOperationException(name)
        });

        Assert.Same(registered, await Resolve(publicCatalog, localCatalog, "kev-0.8b"));

        Task<IModel> Register(object?[] args)
        {
            Assert.Equal("elbruno-maf-foundrylocal-kev-cpu-typed-decision:2", args[1]);
            registrationAttempted = true;
            throw new FoundryLocalException("Registration already exists.");
        }
    }

    [Fact]
    public async Task Resolve_RejectsMissingAndNonDecisionModels()
    {
        var localCatalog = Catalog((name, _) => throw new InvalidOperationException(name));
        var missing = Catalog((_, _) => Task.FromResult<IModel?>(null));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolve(missing, localCatalog, "missing"));
        Assert.Contains("missing", error.Message);

        var chat = PublicCatalog(Model("chat", "chat:1", ModelPath), "chat");
        await Assert.ThrowsAsync<NotSupportedException>(() => Resolve(chat, localCatalog, "chat"));
    }

    [Theory]
    [InlineData("invalid-id", ModelPath, "invalid model ID")]
    [InlineData("kev-cpu:2", "", "cache path")]
    public async Task Resolve_RejectsInvalidModelMetadata(string id, string path, string message)
    {
        var source = Model("decision", id, path);
        var publicCatalog = PublicCatalog(source, "kev-0.8b");
        var localCatalog = Catalog((name, _) => throw new InvalidOperationException(name));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolve(publicCatalog, localCatalog, "kev-0.8b"));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public async Task Resolve_PropagatesCatalogCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var publicCatalog = Catalog((_, args) =>
        {
            Assert.Equal(cancellation.Token, args[1]);
            return Task.FromCanceled<IModel?>(cancellation.Token);
        });
        var localCatalog = Catalog((name, _) => throw new InvalidOperationException(name));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FoundryLocalDecisionModelResolver.ResolveAsync(
                publicCatalog, localCatalog, "kev-0.8b", false, () => { }, cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolve_RespectsDownloadPolicy(bool allowDownload)
    {
        var downloaded = false;
        var notified = false;
        var source = Model("decision", "kev-cpu:2", ModelPath, cached: false,
            onDownload: () => downloaded = true);
        var registered = Model("typed-decision", "elbruno-maf-foundrylocal-kev-cpu-typed-decision:2", ModelPath);
        var publicCatalog = PublicCatalog(source, "kev-0.8b");
        var localCatalog = Catalog((name, _) => name switch
        {
            "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
            "GetModelAsync" => Task.FromResult<IModel?>(null),
            "RegisterModelAsync" => (object)Task.FromResult(registered),
            _ => throw new InvalidOperationException(name)
        });
        var task = FoundryLocalDecisionModelResolver.ResolveAsync(
            publicCatalog, localCatalog, "kev-0.8b", allowDownload, () => notified = true, default);

        if (allowDownload)
        {
            Assert.Same(registered, await task);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.Contains("DownloadIfMissing", error.Message);
        }
        Assert.Equal(allowDownload, downloaded);
        Assert.Equal(allowDownload, notified);
    }

    private static Task<IModel> Resolve(ICatalog publicCatalog, ICatalog localCatalog, string alias)
        => FoundryLocalDecisionModelResolver.ResolveAsync(
            publicCatalog, localCatalog, alias, false, () => throw new InvalidOperationException(), default);

    private static ICatalog Catalog(Func<string, object?[], object?> handler)
        => StubProxy<ICatalog>.Create(handler);

    private static ICatalog PublicCatalog(IModel model, string alias)
        => Catalog((name, args) => name switch
        {
            "GetModelAsync" when Equals(args[0], alias) => Task.FromResult<IModel?>(model),
            "GetModelAsync" => Task.FromResult<IModel?>(null),
            "GetModelVariantAsync" => Task.FromResult<IModel?>(null),
            _ => throw new InvalidOperationException(name)
        });

    private static IModel Model(
        string task, string id, string path, bool cached = true, Action? onDownload = null)
    {
        var info = new ModelInfo
        {
            Id = id,
            Name = id,
            Alias = id.Split(':', 2)[0],
            ProviderType = "local",
            Uri = path,
            ModelType = "onnx",
            Task = task,
            Runtime = new Microsoft.AI.Foundry.Local.Runtime
            {
                DeviceType = DeviceType.CPU,
                ExecutionProvider = "CPUExecutionProvider"
            },
            ContextLength = 4096
        };
        return StubProxy<IModel>.Create((name, _) => name switch
        {
            "get_Id" => id,
            "get_Alias" => id.Split(':', 2)[0],
            "get_Info" => info,
            "IsCachedAsync" => Task.FromResult(cached),
            "GetPathAsync" => Task.FromResult(path),
            "DownloadAsync" => Download(),
            _ => throw new InvalidOperationException(name)
        });

        Task Download()
        {
            onDownload?.Invoke();
            return Task.CompletedTask;
        }
    }
}

public class StubProxy<T> : DispatchProxy where T : class
{
    private Func<string, object?[], object?> _handler = null!;

    public static T Create(Func<string, object?[], object?> handler)
    {
        var instance = DispatchProxy.Create<T, StubProxy<T>>();
        ((StubProxy<T>)(object)instance)._handler = handler;
        return instance;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => _handler(targetMethod!.Name, args ?? []);
}
