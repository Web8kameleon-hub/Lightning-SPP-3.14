# 🤝 Contributing to Lightning SPP

First off, thank you for considering contributing to Lightning SPP! 🎉

Lightning SPP is a professional-grade open-source project, and we welcome contributions from the community.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Pull Request Process](#pull-request-process)
- [Coding Guidelines](#coding-guidelines)
- [Testing](#testing)
- [Documentation](#documentation)

---

## Code of Conduct

This project adheres to the Contributor Covenant [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

---

## How Can I Contribute?

### 🐛 Reporting Bugs

Before creating bug reports, please check the [existing issues](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues) to avoid duplicates.

**When submitting a bug report, include:**

- Lightning SPP version (`dotnet list package | grep LightningSPP`)
- .NET version (`dotnet --version`)
- Operating system and version
- Steps to reproduce
- Expected behavior
- Actual behavior
- Minimal code example
- Stack trace (if applicable)

**Template:**
```markdown
**Environment:**
- Lightning SPP: 3.14.0
- .NET: 10.0.1
- OS: Windows 11 x64

**Steps to Reproduce:**
1. ...
2. ...

**Expected:** ...
**Actual:** ...

**Code:**
```csharp
// Minimal reproducible example
```
```

### 💡 Suggesting Features

We love feature requests! Submit them as [GitHub Discussions](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions/categories/ideas).

**When suggesting features, include:**

- **Use case**: Why do you need this feature?
- **Proposed solution**: How should it work?
- **Alternatives**: What alternatives have you considered?
- **Impact**: Who benefits from this feature?

### 📝 Improving Documentation

Documentation improvements are always welcome!

- Fix typos
- Clarify confusing sections
- Add examples
- Translate to other languages
- Create tutorials

### 💻 Code Contributions

#### Good First Issues

Look for issues labeled [`good first issue`](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/labels/good%20first%20issue) - perfect for newcomers!

#### Areas We Need Help With

- Performance optimizations
- Additional device profiles
- Integration examples
- Unit tests
- Benchmarks
- Regulated-workflow validation and compliance evidence

---

## Development Setup

### Prerequisites

1. **Install .NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0
2. **Install Git**: https://git-scm.com/downloads
3. **Install IDE** (choose one):
   - Visual Studio 2022 17.8+ or Visual Studio 2026
   - Visual Studio Code with C# extension
   - JetBrains Rider 2024.3+

### Fork and Clone

1. **Fork** the repository on GitHub
2. **Clone** your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/Lightning-SPP-3.14.git
   cd Lightning-SPP-3.14
   ```

3. **Add upstream** remote:
   ```bash
   git remote add upstream https://github.com/Web8kameleon-hub/Lightning-SPP-3.14.git
   ```

### Build

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run demo
dotnet run -- demo
```

### Project Structure

```
Lightning-SPP-3.14/
├── Services/              # Core service interfaces
│   ├── IScanService.cs
│   ├── IProcessingService.cs
│   └── IPrintService.cs
├── Services/Implementation/  # Service implementations
│   ├── RealImageProcessor.cs    # REAL: ImageSharp
│   ├── RealAIProcessor.cs       # REAL: ML
│   ├── RealPrintService.cs      # REAL: File I/O
│   ├── FastScanService.cs       # Compatibility wrapper
│   ├── HybridProcessingService.cs  # Compatibility wrapper
│   └── StigmaPrintService.cs    # Compatibility wrapper
├── SDK/                   # Public SDKs
│   ├── Lightning314.cs
│   └── UniversalSDK.cs
├── PublicAPI/             # Main API entry point
│   └── LightningAPI.cs
├── Licensing/             # License system
│   ├── LicenseKey.cs
│   └── LicenseManager.cs
├── Configuration/         # Configuration
│   └── LightningConfiguration.cs
├── Examples/              # Usage examples
├── Tests/                 # Unit tests
└── Docs/                  # Documentation
```

---

## Pull Request Process

### 1. Create a Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/bug-description
```

**Branch naming conventions:**
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation only
- `perf/` - Performance improvements
- `test/` - Test additions/improvements
- `refactor/` - Code refactoring

### 2. Make Changes

- Write clean, readable code
- Follow [coding guidelines](#coding-guidelines)
- Add tests for new features
- Update documentation

### 3. Test Your Changes

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~YourTestName"

# Run benchmarks
dotnet run -- speed

# Test REAL processing
dotnet run -- real
```

### 4. Commit

```bash
git add .
git commit -m "feat: Add your feature description"
```

**Commit message format:**
```
<type>: <description>

[optional body]

[optional footer]
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, missing semicolons, etc.
- `refactor`: Code restructuring
- `perf`: Performance improvement
- `test`: Adding tests
- `chore`: Maintenance

**Examples:**
```
feat: Add batch processing for IoT devices

Implements parallel batch processing optimized for
constrained IoT environments with memory limits.

Closes #123
```

```
fix: Resolve null reference in LicenseManager

GetActiveLicense() was returning null when license
file was corrupted. Now handles gracefully.

Fixes #456
```

### 5. Push and Create PR

```bash
git push origin feature/your-feature-name
```

Then create a Pull Request on GitHub.

### 6. PR Review Process

- Maintainers will review within **72 hours**
- Address feedback promptly
- Once approved, your PR will be merged!

---

## Coding Guidelines

### C# Style

Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Key Points:**

1. **Naming:**
   ```csharp
   // PascalCase for public members
   public class LightningProcessor { }
   public void ProcessAsync() { }
   
   // camelCase for private fields
   private readonly int _maxRetries;
   
   // UPPER_CASE for constants
   private const int MAX_BUFFER_SIZE = 1024;
   ```

2. **Async/Await:**
   ```csharp
   // Always use Async suffix
   public async Task<Result> ProcessAsync(byte[] data)
   {
       // Prefer ConfigureAwait(false) in library code
       var result = await SomeOperationAsync().ConfigureAwait(false);
       return result;
   }
   ```

3. **Nullability:**
   ```csharp
   // Enable nullable reference types
   #nullable enable
   
   // Use nullable annotations
   public string? NullableString { get; set; }
   public string NonNullableString { get; set; } = string.Empty;
   ```

4. **Comments:**
   ```csharp
   /// <summary>
   /// XML documentation for public APIs
   /// </summary>
   /// <param name="data">Input data to process</param>
   /// <returns>Processed result</returns>
   public async Task<Result> ProcessAsync(byte[] data)
   {
       // Inline comments for complex logic
       var compressed = CompressData(data); // Uses Brotli
       return new Result(compressed);
   }
   ```

### Performance

- **Avoid allocations** in hot paths
- **Use pooling** for frequently allocated objects
- **Prefer ValueTask** over Task when appropriate
- **Profile** before optimizing

```csharp
// Good: Use pooling
using var buffer = MemoryPool<byte>.Shared.Rent(size);

// Good: Use Span for stack allocations
Span<byte> stackBuffer = stackalloc byte[256];

// Good: Use ValueTask for frequently called methods
public ValueTask<int> GetCachedValueAsync()
{
    return _cache.TryGetValue(key, out var value)
        ? new ValueTask<int>(value)
        : new ValueTask<int>(ComputeAsync());
}
```

### Error Handling

```csharp
// Return results instead of exceptions for expected failures
public record Result(bool Success, string Error);

public async Task<Result> ProcessAsync(byte[] data)
{
    if (data == null || data.Length == 0)
        return new Result(false, "Data cannot be empty");
    
    try
    {
        // Process...
        return new Result(true, string.Empty);
    }
    catch (Exception ex)
    {
        // Log exception
        _logger.LogError(ex, "Processing failed");
        return new Result(false, ex.Message);
    }
}
```

---

## Testing

### Unit Tests

```csharp
using Xunit;

public class LightningProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var processor = new LightningProcessor();
        var data = new byte[1024];
        
        // Act
        var result = await processor.ProcessAsync(data);
        
        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public async Task ProcessAsync_WithInvalidData_ReturnsFailure(byte[]? data)
    {
        // Arrange
        var processor = new LightningProcessor();
        
        // Act
        var result = await processor.ProcessAsync(data);
        
        // Assert
        Assert.False(result.Success);
    }
}
```

### Integration Tests

```csharp
[Fact]
public async Task FullPipeline_Integration_Test()
{
    // Arrange
    LightningAPI.UseRealServices();
    var data = File.ReadAllBytes("test-image.jpg");
    
    // Act
    var result = await LightningAPI.ExecuteLightningPipelineAsync(
        data,
        ScanMode.Lightning,
        ProcessingEngine.Hybrid,
        PrintQuality.High
    );
    
    // Assert
    Assert.True(result.Success);
    Assert.InRange(result.TotalTimeMs, 0, 100); // Performance assertion
}
```

### Benchmarks

```csharp
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class ProcessingBenchmarks
{
    private byte[] _data = null!;
    
    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[1024 * 100]; // 100KB
    }
    
    [Benchmark]
    public async Task<Result> Lightning_Mode()
    {
        return await Lightning314.Lightning(_data);
    }
    
    [Benchmark]
    public async Task<Result> Instant_Mode()
    {
        return await Lightning314.Instant(_data);
    }
}
```

---

## Documentation

### Code Documentation

- **Public APIs** must have XML documentation
- **Complex algorithms** need inline comments
- **Examples** for non-obvious usage

### Wiki Documentation

Update Wiki when adding:
- New features → Update Home.md, API-Documentation.md
- New device profiles → Update Device-Optimization.md
- Breaking changes → Update Migration-Guide.md
- Common errors → Update Troubleshooting.md

### README Updates

Keep README.md up to date with:
- New features
- Changed APIs
- Updated examples

---

## Questions?

- **GitHub Discussions**: https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions
- **Email**: clisonix@pm.me

---

**Thank you for contributing to Lightning SPP!** 🚀

---

*© 2025-2026 ABA GmbH. All rights reserved.*
