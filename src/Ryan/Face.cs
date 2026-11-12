namespace SteveDaysToDie.Ryan;

public static class Face
{
	public const int Up = 0;

	public const int Down = 1;

	public const int North = 2;

	public const int South = 3;

	public const int West = 4;

	public const int East = 5;

	public static readonly string[] Names = new string[6] { "up", "down", "north", "south", "west", "east" };

	public static readonly Vector3i[] Dir = new Vector3i[6]
	{
		new Vector3i(0, 1, 0),
		new Vector3i(0, -1, 0),
		new Vector3i(0, 0, 1),
		new Vector3i(0, 0, -1),
		new Vector3i(-1, 0, 0),
		new Vector3i(1, 0, 0)
	};

	public static readonly int[] Ring = new int[4] { 2, 5, 3, 4 };

	public static int FromBlockFace(BlockFace f)
	{
		return f switch
		{
			BlockFace.Top => 0, 
			BlockFace.Bottom => 1, 
			BlockFace.North => 2, 
			BlockFace.South => 3, 
			BlockFace.West => 4, 
			BlockFace.East => 5, 
			_ => 0, 
		};
	}
}
