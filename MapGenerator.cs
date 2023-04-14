using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fully functional random map generator. Requires MeshGenerator.cs though.

public class MapGenerator : MonoBehaviour
{
    public int width;
    public int height;
    public int smoothing;
    public int wallSizeMinimum;
    public int roomSizeMinimum;
    public float fillPercentage;
    public string seed;
    public bool randomSeed;
    public bool regenerate;

    int[,] map;

    void Start()
    {
        GenerateMap();
    }

    // Used for debugging. Allows recreation of the map while the game is playing.
    void Update()
    {
        if(regenerate)
        {
            regenerate = false;
            GenerateMap();
        }
    }

    // Gets a region of a specific tiletype (i.e. a region of walls, or a room)
    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];

        for(int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                if(mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach(Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    // Gets a list of all tiles in the same region.
    List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while(queue.Count > 0)
        {
            Coord tile = queue.Dequeue(); // Gets the first item in the queue and removes it from the queue.
            tiles.Add(tile);

            for(int x = tile.tileX-1; x <= tile.tileX+1; x++)
            {
                for(int y = tile.tileY-1; y <= tile.tileY+1; y++)
                {
                    if(isInMapRange(x, y) && (x == tile.tileX || y == tile.tileY))
                    {
                        if(mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    // Checks whether or not a specific point is within the map boundries.
    bool isInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Generate the entire map.
    void GenerateMap()
    {
        if(randomSeed)
        {
            seed = System.DateTime.Now.ToString();
        }

        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        map = new int[width, height];

        // Loop and fill tiles, creating noise.
        for(int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                if(x == 0 || x == width-1 || y == 0 || y == height-1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < fillPercentage) ? 1 : 0;
                }
            }
        }

        // Smooth the generation to make it actually create something nice.
        for(int s = 0; s < smoothing; s++)
        {
            for(int x = 0; x < width; x++)
            {
                for(int y = 0; y < height; y++)
                {
                    int neighbourWallCount = GetNeighbourCount(x, y);
                    if(neighbourWallCount > 4)
                    {
                        map[x, y] = 1;
                    }
                    else if(neighbourWallCount < 4)
                    {
                        map[x, y] = 0;
                    }
                }
            }
        }

        ProcessMap(); // Used to remove small walls / rooms.

        // Creates the mesh with the help of the MeshGenerator script.
        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(map, 1);
    }

    // Fine tunes the map, removing certain regions if they're too small.
    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1);
        foreach(List<Coord> wallRegion in wallRegions)
        {
            if(wallRegion.Count < wallSizeMinimum)
            {
                foreach(Coord tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = 0;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(0);
        List<Room> survivingRooms = new List<Room>();
        foreach(List<Coord> roomRegion in roomRegions)
        {
            if(roomRegion.Count < roomSizeMinimum)
            {
                foreach(Coord tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = 1;
                }
            }
            else
            {
                survivingRooms.Add(new Room(roomRegion, map));
            }
        }

        survivingRooms.Sort();
        survivingRooms[0].isMainRoom = true;
        survivingRooms[0].isAccessableFromMainRoom = true;

        ConnectClosestRooms(survivingRooms);
    }

    void ConnectClosestRooms(List<Room> allRooms, bool forceAccessabilityFromMainRoom = false)
    {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if(forceAccessabilityFromMainRoom)
        {
            foreach(Room room in allRooms)
            {
                if(room.isAccessableFromMainRoom)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }
        }
        else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach(Room roomA in roomListA)
        {
            if(!forceAccessabilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if(roomA.connectedRooms.Count > 0){continue;}
            }

            foreach(Room roomB in roomListB)
            {
                if(roomA == roomB || roomA.IsConnected(roomB)){continue;} // Don't compare a room with itself.



                for(int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for(int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX-tileB.tileX, 2) + Mathf.Pow(tileA.tileY-tileB.tileY, 2));

                        if(distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }

            if(possibleConnectionFound && !forceAccessabilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

            if(possibleConnectionFound && forceAccessabilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
                ConnectClosestRooms(allRooms, true);
            }

        if(!forceAccessabilityFromMainRoom)
        {
            ConnectClosestRooms(allRooms, true);
        }
    }

    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);
        //Debug.DrawLine(CoordToWorldPoint(tileA), CoordToWorldPoint(tileB), Color.green, 100.0f); // SHows lines where paths have been created.

        List<Coord> line = GetLine(tileA, tileB);
        foreach(Coord c in line)
        {
            DrawCircle(c, UnityEngine.Random.Range(0, 4+1));
        }
    }

    void DrawCircle(Coord c, int radius)
    {
        for(int x = -radius; x <= radius; x++)
        {
            for(int y = -radius; y <= radius; y++)
            {
                if(x*x + y*y <= radius*radius)
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if(isInMapRange(drawX, drawY))
                    {
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if(longest < shortest)
        {
            inverted = true;

            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for(int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));
            if(inverted)
            {
                y += step;
            }
            else
            {
                x += step;
            }

            gradientAccumulation += shortest;

            if(gradientAccumulation >= longest)
            {
                if(inverted)
                {
                    x += gradientStep;
                }
                else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    Vector3 CoordToWorldPoint(Coord tile)
    {
        return new Vector3(-width / 2 + 0.5f + tile.tileX, 0, -height / 2 + 0.5f + tile.tileY);
    }

    // Get the neighbour count of a tile.
    int GetNeighbourCount(int gridX, int gridY)
    {
        int count = 0;
        for(int neighbourX = gridX-1; neighbourX <= gridX+1; neighbourX++)
        {
            for(int neighbourY = gridY-1; neighbourY <= gridY+1; neighbourY++)
            {
                if(isInMapRange(neighbourX, neighbourY)) // Make sure we're looping within map bounds.
                {
                    if(neighbourX != gridX || neighbourY != gridY) // Ignore self.
                    {
                        count += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    count++; // Increase count by the map edges.
                }
            }
        }
        return count;
    }

    // A struct which handles tile information at a specific coordinate.
    struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }

    class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessableFromMainRoom;
        public bool isMainRoom;

        // In case we want to create an empty room.
        public Room(){}

        public Room(List<Coord> roomTiles, int[,] map)
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();

            edgeTiles = new List<Coord>();

            // Find out if a tile is an edge tile.
            foreach(Coord tile in tiles)
            {
                for(int x = tile.tileX-1; x <= tile.tileX+1; x++)
                {
                    for(int y = tile.tileY-1; y <= tile.tileY+1; y++)
                    {
                        // Check only adjecent tiles, not diagonal ones.
                        if(x == tile.tileX || y == tile.tileY)
                        {
                            if(map[x, y] == 1)
                            {
                                edgeTiles.Add(tile);
                            }
                        }
                    }
                }
            }
        }

        public void SetAccessableFromMainRoom()
        {
            if(!isAccessableFromMainRoom)
            {
                isAccessableFromMainRoom = true;
                foreach(Room cRoom in connectedRooms)
                {
                    cRoom.SetAccessableFromMainRoom();
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if(roomA.isAccessableFromMainRoom)
            {
                roomB.SetAccessableFromMainRoom();
            }
            else if(roomB.isAccessableFromMainRoom)
            {
                roomA.SetAccessableFromMainRoom();
            }
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    /* Used for early stage debugging.
    void OnDrawGizmos()
    {
        if(map != null)
        {
            for(int x = 0; x < width; x++)
            {
                for(int y = 0; y < height; y++)
                {
                    Gizmos.color = (map[x, y] == 1) ? Color.black : Color.white;
                    Vector3 pos = new Vector3(-width/2 + x + 0.5f, 0, -height/2 + y + 0.5f);
                    Gizmos.DrawCube(pos, Vector3.one);
                }
            }
        }
    }
    */
}