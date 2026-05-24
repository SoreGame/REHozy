namespace REHozy.Decoration
{
    public static class DecorationCarrySession
    {
        public static PlaceableDecoration Active { get; private set; }

        public static bool IsCarrying => Active != null;

        public static void SetActive(PlaceableDecoration decoration)
        {
            Active = decoration;
        }

        public static void Clear(PlaceableDecoration decoration)
        {
            if (Active == decoration)
            {
                Active = null;
            }
        }
    }
}
