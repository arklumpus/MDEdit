using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace MDEditDemo
{
    public class MainWindow : Window
    {
        MDEdit.Editor Editor;

        public MainWindow()
        {
            InitializeComponent();

            // This is necessary to support raster images embedded within SVG files.
            VectSharp.SVG.Parser.ParseImageURI = VectSharp.MuPDFUtils.ImageURIParser.Parser(VectSharp.SVG.Parser.ParseSVGURI);

            this.Opened += async (s, e) =>
            {
                // Initial source code: the VectSharp.Markdown readme.
                string sourceText = "";
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    sourceText = client.DownloadString("https://raw.githubusercontent.com/arklumpus/VectSharp/master/VectSharp.Markdown/Readme.md");
                }

                Editor = await MDEdit.Editor.Create(sourceText);

                // This is necessary to support raster images embedded directly in the Markdown document.
                Editor.MarkdownRenderer.RasterImageLoader = imageFile => new VectSharp.MuPDFUtils.RasterImageFile(imageFile);

                // This uri will be used to resolve image addresses: we need them to point at the raw files
                Editor.MarkdownRenderer.BaseImageUri = "https://raw.githubusercontent.com/arklumpus/VectSharp/master/VectSharp.Markdown/";

                // This uri will be used to resolve link addresses: in this case, we don't want to point to the raw files, but to the GitHub preview
                Editor.MarkdownRenderer.BaseLinkUri = new Uri("https://github.com/arklumpus/VectSharp/blob/master/VectSharp.Markdown/");

                this.Content = Editor;
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}