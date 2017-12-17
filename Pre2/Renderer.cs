using Tilengine;

namespace Pre2
{
    public sealed class Renderer
    {
        public static Renderer Instance { get; } = new Renderer();

        public const int Hres = 320;
        public const int Vres = 200;

        public Engine Engine;
        public Window Window;

        private Layer foreground;

        private Renderer()
        {
            Engine = Engine.Init(Hres, Vres, 1, 0, 0);
            foreground = Engine.Layers[0];
            Engine.LoadPath = AssetConverter.CacheDir;

            Window = Window.Create(null, WindowFlags.Vsync | WindowFlags.S5);
            Window.DisableCRTEffect();
        }

        public void SetBackgroundBitmap(Bitmap bitmap)
        {
            Engine.BackgroundBitmap = bitmap;
        }

        public void SetLevelTilemap(Tilemap tilemap)
        {
            foreground.SetMap(tilemap);
        }

        public void SetForegroundPosition(int x, int y)
        {
            foreground.SetPosition(x, y);
        }
    }
}
