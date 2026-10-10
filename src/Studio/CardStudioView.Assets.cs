using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace CardStudio;

public sealed partial class CardStudioView
{
    private sealed record ImageAsset(string Id, string Name, string Base64)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Bitmap? Preview { get; set; }
    }

    private readonly List<ImageAsset> _imageAssets = [];
    private readonly Dictionary<string, Bitmap> _assetBitmaps = [];
    private ListBox _imageAssetsList = null!;
    private bool _assetsRestored;
    private static string AssetFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CardStudio", "images.json");

    private void InitializeImageAssets()
    {
        _imageAssetsList = this.FindControl<ListBox>("ImageAssetsList")!;
        this.FindControl<TextBlock>("ImageAssetsHint")!.Text = OperatingSystem.IsBrowser()
            ? "保存在此浏览器中，刷新后仍可使用。"
            : "保存在本机，下次打开仍可使用。";
        this.FindControl<Button>("ImportImagesButton")!.Click += async (_, _) => await ImportImagesAsync();
        this.FindControl<Button>("ClearImagesButton")!.Click += async (_, _) =>
        {
            try
            {
                await SaveImageAssetsAsync([]);
                foreach (var (key, bitmap) in _assetBitmaps)
                {
                    Application.Current!.Resources.Remove(key);
                    bitmap.Dispose();
                }
                _assetBitmaps.Clear();
                _imageAssets.Clear();
                RefreshAssetList();
                RenderPreview();
                _statusText.Text = "图片资源已清空";
            }
            catch (Exception ex) { _statusText.Text = "清理失败：" + GetUsefulError(ex); }
        };
    }

    private void InsertImageAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not ImageAsset row) return;
        var asset = _imageAssets.FirstOrDefault(x => x.Base64 == row.Base64);
        if (asset is null) return;
        this.FindControl<TabControl>("EditorTabs")!.SelectedIndex = 0;
        _codeEditor.Document.Insert(_codeEditor.CaretOffset,
            $"<Image Source=\"{{DynamicResource {asset.Id}}}\" Stretch=\"Uniform\" />");
        _codeEditor.Focus();
    }

    private async void DeleteImageAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not ImageAsset row) return;
        var asset = _imageAssets.FirstOrDefault(x => x.Base64 == row.Base64);
        if (asset is null) return;
        _imageAssets.Remove(asset);
        Application.Current!.Resources.Remove(asset.Id);
        if (_assetBitmaps.Remove(asset.Id, out var bitmap)) bitmap.Dispose();
        try
        {
            await SaveImageAssetsAsync(_imageAssets);
            RefreshAssetList();
            RenderPreview();
            _statusText.Text = $"已删除图片 {asset.Name}";
        }
        catch (Exception ex) { _statusText.Text = "删除失败：" + GetUsefulError(ex); }
    }

    private async void ImageAssetNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await CommitImageAssetNameAsync(sender as TextBox);
    }

    private async void ImageAssetNameLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await CommitImageAssetNameAsync(sender as TextBox);

    private async Task CommitImageAssetNameAsync(TextBox? editor)
    {
        if (editor?.DataContext is not ImageAsset asset) return;
        var newName = editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(newName)) return;

        var stem = Path.GetFileNameWithoutExtension(newName);
        var newId = SanitizeAssetId(stem);
        if (newName == asset.Name && newId == asset.Id) return;
        if (_imageAssets.Any(x => x.Id == newId && x.Id != asset.Id))
        {
            _statusText.Text = "图片名对应的资源键已存在，请换一个名称";
            editor.Text = asset.Name;
            return;
        }

        try
        {
            var renamed = asset with { Id = newId, Name = newName };
            var index = _imageAssets.IndexOf(asset);
            var bitmap = _assetBitmaps[asset.Id];
            Application.Current!.Resources.Remove(asset.Id);
            Application.Current.Resources[newId] = bitmap;
            _assetBitmaps.Remove(asset.Id);
            _assetBitmaps[newId] = bitmap;
            _imageAssets[index] = renamed;
            _codeEditor.Document.Text = _codeEditor.Document.Text.Replace(
                $"{{DynamicResource {asset.Id}}}", $"{{DynamicResource {newId}}}");
            await SaveImageAssetsAsync(_imageAssets);
            RefreshAssetList();
            _imageAssetsList.SelectedItem = renamed;
            _statusText.Text = $"图片资源已重命名为 {newName}";
        }
        catch (Exception ex)
        {
            _statusText.Text = "重命名失败：" + GetUsefulError(ex);
            editor.Text = asset.Name;
        }
    }

    private async Task RestoreImageAssetsAsync()
    {
        if (_assetsRestored) return;
        _assetsRestored = true;
        try
        {
            var json = OperatingSystem.IsBrowser() ? await BrowserAssetStorage.LoadAsync()
                : File.Exists(AssetFile) ? await File.ReadAllTextAsync(AssetFile) : "[]";
            var migrated = false;
            foreach (var storedAsset in JsonSerializer.Deserialize<List<ImageAsset>>(json) ?? [])
            {
                var asset = storedAsset;
                var cleanName = Path.GetFileNameWithoutExtension(asset.Name);
                if (cleanName != asset.Name)
                {
                    asset = asset with { Name = cleanName };
                    migrated = true;
                }
                var desiredId = SanitizeAssetId(asset.Name);
                if (asset.Id != desiredId)
                {
                    var oldId = asset.Id;
                    asset = asset with { Id = CreateUniqueAssetId(asset.Name) };
                    _codeEditor.Document.Text = _codeEditor.Document.Text.Replace(
                        $"{{DynamicResource {oldId}}}", $"{{DynamicResource {asset.Id}}}");
                    migrated = true;
                }
                RegisterImageAsset(asset);
            }
            if (migrated) await SaveImageAssetsAsync(_imageAssets);
            RefreshAssetList();
        }
        catch (Exception ex) { _statusText.Text = "恢复图片失败：" + GetUsefulError(ex); }
    }

    private async Task SaveImageAssetsAsync(IEnumerable<ImageAsset> assets)
    {
        var json = JsonSerializer.Serialize(assets);
        if (OperatingSystem.IsBrowser()) await BrowserAssetStorage.SaveAsync(json);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetFile)!);
            await File.WriteAllTextAsync(AssetFile, json);
        }
    }

    private void RegisterImageAsset(ImageAsset asset)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(asset.Base64));
        var bitmap = new Bitmap(stream);
        asset.Preview = bitmap;
        Application.Current!.Resources[asset.Id] = bitmap;
        _assetBitmaps.Add(asset.Id, bitmap);
        _imageAssets.Add(asset);
    }

    private void RefreshAssetList()
    {
        _imageAssetsList.ItemsSource = _imageAssets.ToArray();
        if (_imageAssets.Count > 0) _imageAssetsList.SelectedIndex = _imageAssets.Count - 1;
    }

    private async Task ImportImagesAsync()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "导入图片", AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("图片")
                { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"], MimeTypes = ["image/*"] }]
            });
            foreach (var file in files)
            {
                await using var source = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await source.CopyToAsync(buffer);
                var name = Path.GetFileNameWithoutExtension(file.Name);
                RegisterImageAsset(new ImageAsset(CreateUniqueAssetId(name),
                    name, Convert.ToBase64String(buffer.ToArray())));
            }
            RefreshAssetList();
            await SaveImageAssetsAsync(_imageAssets);
            _statusText.Text = $"已保存 {_imageAssets.Count} 个图片资源 · 选择图片后插入 AXAML";
        }
        catch (Exception ex)
        {
            RefreshAssetList();
            _statusText.Text = "图片导入或保存失败：" + GetUsefulError(ex);
        }
    }

    private string CreateUniqueAssetId(string fileName)
    {
        var baseId = SanitizeAssetId(Path.GetFileNameWithoutExtension(fileName));
        var candidate = baseId;
        var suffix = 2;
        while (_imageAssets.Any(x => x.Id == candidate)) candidate = $"{baseId}_{suffix++}";
        return candidate;
    }

    private static string SanitizeAssetId(string value)
    {
        var id = Regex.Replace(value, @"[^A-Za-z0-9_]", "_");
        if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"^[A-Za-z_]")) id = "Image_" + id;
        return id;
    }
}
