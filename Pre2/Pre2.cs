using Tilengine;

namespace Pre2
{
    class Pre2
    {
        static Renderer renderer;
        static Window window;

        static float posForegroundX;
        static float posForegroundY;

        private static int frame;

        private static float UpdateSpeedCoord(float speed, int dir)
        {
            switch (dir)
            {
                case 1:
                    speed += 0.02f;
                    if (speed > 1.0f) { speed = 1.0f; }
                    break;

                case -1:
                    speed -= 0.02f;
                    if (speed < -1.0f) { speed = -1.0f; }
                    break;

                default:
                    if (speed > 0.0f)
                    {
                        speed -= 0.02f;
                        if (speed < 0.0f) { speed = 0.0f; }
                    }
                    else if (speed < 0.0f)
                    {
                        speed += 0.02f;
                        if (speed > 0.0f) { speed = 0.0f; }
                    }
                    break;
            }
            return speed;
        }

        static void SetLevelTilemap(int levelIdx)
        {
            Tilemap tilemap = AssetConverter.GetLevelTilemap(levelIdx);
            renderer.SetLevelTilemap(tilemap);
        }

        static void SetLevelBackground(int levelIdx)
        {
            Bitmap bitmap = AssetConverter.GetLevelBackground(levelIdx);
            renderer.SetBackgroundBitmap(bitmap);
        }

        static int Main()
        {
            AssetConverter.PrepareAllAssets();

            renderer = Renderer.Instance;

            window = renderer.Window;

            ShowTitusScreen();

            int levelIdx = 0;
            PlayLevel(levelIdx);

            renderer.Engine.Deinit();
            return 0;
        }

        private static void PlayLevel(int levelIdx)
        {
            float speedX = 0;
            float speedY = 0;

            SetLevelTilemap(levelIdx);
            SetLevelBackground(levelIdx);

            while (window.Process())
            {
                int dirX = 0;
                int dirY = 0;
                if (window.GetInput(Input.Right)) { dirX++; }
                if (window.GetInput(Input.Left))  { dirX--; }
                if (window.GetInput(Input.Down))  { dirY++; }
                if (window.GetInput(Input.Up))    { dirY--; }
                speedX = UpdateSpeedCoord(speedX, dirX);
                speedY = UpdateSpeedCoord(speedY, dirY);

                posForegroundX += 3 * speedX;
                posForegroundY += 3 * speedY;
                renderer.SetForegroundPosition((int)posForegroundX, (int)posForegroundY);

                window.DrawFrame(frame++);
            }
        }

        private static void ShowTitusScreen()
        {
            renderer.SetBackgroundBitmap(AssetConverter.GetTitusBitmap());
            while (window.Process())
            {
                if (window.GetInput(Input.Down))
                {
                    break;
                }
                window.DrawFrame(frame++);
            }
        }
    }
}
