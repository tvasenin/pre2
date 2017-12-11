using Tilengine;

namespace Pre2
{
    public class GameSession
    {
        private Engine engine;

        private int levelIdx;
        private byte numLife1Ups = 2;
        private byte bonusItemsBitmap = 0;
        private byte foodItemsMap = 0;
        private bool isExpertMode;
        private byte weaponStrength = 20;
        private byte weaponType = 0;

        public GameSession(Engine engine, int levelIdx, bool isExpertMode = false)
        {
            this.engine = engine;
            this.levelIdx = levelIdx;
            this.isExpertMode = isExpertMode;
        }
    }
}
