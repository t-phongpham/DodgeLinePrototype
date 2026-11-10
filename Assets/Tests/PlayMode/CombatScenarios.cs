namespace DodgeLine.Tests
{
    // Small authored scenarios for isolated combat regression tests, not shipping levels.
    internal static class CombatScenarios
    {
        public static WaveDefinition[] WispSpacing(LevelSettings s) => new[] {
            new WaveDefinition("Wisp spacing fixture", new WaveSpawn(s.wisp, 0), new WaveSpawn(s.wisp, 3), new WaveSpawn(s.wisp, 3),
                new WaveSpawn(s.runner, 6), new WaveSpawn(s.runner, 1.3f), new WaveSpawn(s.runner, 1.3f)) };
        public static WaveDefinition[] TwoLaneCombat(LevelSettings s)
        {
            var r = s.runner; var w = s.wisp; var g = s.ironGuard;
            return new[] {
                new WaveDefinition("Two-lane combat fixture 1", new WaveSpawn(r,0), new WaveSpawn(r,.25f,1), new WaveSpawn(r,.65f), new WaveSpawn(r,.25f,1), new WaveSpawn(w,3.35f), new WaveSpawn(w,.5f,1), new WaveSpawn(g,7,1)),
                new WaveDefinition("Two-lane combat fixture 2", new WaveSpawn(w,0), new WaveSpawn(w,1,1), new WaveSpawn(w,3), new WaveSpawn(w,1,1), new WaveSpawn(g,3), new WaveSpawn(g,.5f,1), new WaveSpawn(r,1.5f), new WaveSpawn(r,.5f,1), new WaveSpawn(r,.5f), new WaveSpawn(r,.5f,1)),
                new WaveDefinition("Two-lane combat fixture 3", new WaveSpawn(w,0), new WaveSpawn(w,.5f,1), new WaveSpawn(w,3.5f), new WaveSpawn(w,.5f,1), new WaveSpawn(g,3.5f), new WaveSpawn(g,.5f,1), new WaveSpawn(g,.5f), new WaveSpawn(g,.5f,1), new WaveSpawn(r,.5f), new WaveSpawn(r,.5f,1), new WaveSpawn(r,.5f), new WaveSpawn(r,.5f,1))
            };
        }
    }
}
