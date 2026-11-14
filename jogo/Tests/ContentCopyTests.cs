namespace CinzasDaMare.Tests;

/// <summary>
/// Guarda contra o conteúdo velho na pasta de saída (aconteceu ao atualizar o projeto por ZIP):
/// os JSON copiados para os testes precisam ser idênticos aos de /Content do repositório.
/// </summary>
public class ContentCopyTests
{
    private static string? FindRepositoryContent()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CinzasDaMare.sln"))
                && Directory.Exists(Path.Combine(dir.FullName, "Content")))
                return Path.Combine(dir.FullName, "Content");
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void ConteudoCopiadoParaOsTestes_EIgualAoDoRepositorio()
    {
        var source = FindRepositoryContent();
        Assert.True(source is not null, "CinzasDaMare.sln não encontrado acima de " + AppContext.BaseDirectory);

        var files = Directory.GetFiles(source!, "*.json");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var copy = Path.Combine(AppContext.BaseDirectory, "Content", Path.GetFileName(file));
            Assert.True(File.Exists(copy), "Não foi copiado: " + copy);
            Assert.Equal(File.ReadAllText(file), File.ReadAllText(copy)); // desatualizado se diferir
        }
    }
}
