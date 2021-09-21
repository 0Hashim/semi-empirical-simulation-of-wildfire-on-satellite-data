using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

public class FireSimulator : MonoBehaviour
{
    public static float thresh_up = 1.1f;
    public static float thresh_down = 0.99f;
    public static float thresh_wind = -0.99f;
    public static float R_0 = 1f;

    public CellState state;

    public Vector2 chunk;
    public Vector2 position;

    EndlessTerrain endlessTerrain;

    void Start()
    {
        endlessTerrain = FindObjectOfType<EndlessTerrain>();
    }

    public void SetFire()
    {
        endlessTerrain.terrainChunkDictionary[chunk].cellGrid.SetFire((int)position.y, (int)position.x);
    }
}

public enum CellState { burnable, not_burnable, ignited, on_fire, extenguishing, fully_extenguished };

public class Cell
{
    public readonly float elevation;
    int land_type;
    float veg_elv;

    public CellState state;
    CellState prev_state;

    public Cell(int land_type, float elevation)
    {
        this.land_type = land_type;
        this.elevation = elevation;
        this.veg_elv = 0f;

        switch (land_type)
        {
            case 0:
                this.state = CellState.burnable;
                break;
            
            case 1:
                this.state = CellState.burnable;
                break;

            case 2:
                this.state = CellState.not_burnable;
                break;

            case 3:
                this.state = CellState.not_burnable;
                break;
        }

        this.prev_state = this.state;
    }

    public void SetState(CellState new_state)
    {
        this.prev_state = this.state;
        this.state = new_state;
    }

    public void UpdateCell()
    {
        if (this.state == CellState.ignited && this.prev_state == CellState.ignited)
        {
            this.SetState(CellState.extenguishing);
            return;
        }

        else if (this.state == CellState.on_fire && this.prev_state == CellState.on_fire)
        {
            this.SetState(CellState.extenguishing);
            return;
        }

        else if (this.state == CellState.extenguishing && this.prev_state == CellState.extenguishing)
        {
            this.SetState(CellState.fully_extenguished);
            return;
        }

        this.prev_state = this.state;
    }
}

public class CellGrid
{
    public List<List<Cell>> grid;
    public List<List<Cell>> new_grid;

    public bool hasIgnitedCell;

    public CellGrid(int[,] vegetation_data, float[,] elevation_data)
    {
        this.grid = new List<List<Cell>>();

        for (int x = 0; x < vegetation_data.GetLength(0); x++)
        {
            List<Cell> row = new List<Cell>();
            for (int y = 0; y < vegetation_data.GetLength(1); y++)
            {
                row.Add(new Cell(vegetation_data[x, y], elevation_data[x, y]));
            }

            this.grid.Add(row);
        }

        this.new_grid = new List<List<Cell>>(this.grid);

        this.hasIgnitedCell = false;
    }

    public List<Cell> Neighbours(int x, int y)
    {
        List<Cell> neighbours = new List<Cell> {
            this.new_grid[x-1][y-1],
            this.new_grid[x-1][y],
            this.new_grid[x-1][y+1],
            this.new_grid[x][y+1],
            this.new_grid[x+1][y+1],
            this.new_grid[x+1][y],
            this.new_grid[x+1][y-1],
            this.new_grid[x][y-1],
        };

        return neighbours;
    }

    public float[,] Direction()
    {
        float[,] direction = new float[,] {
            {1/Mathf.Sqrt(2), -1/Mathf.Sqrt(2)},
            {0, 1},
            {1/Mathf.Sqrt(2), 1/Mathf.Sqrt(2)},
            {1, 0},
            {1/Mathf.Sqrt(2), -1/Mathf.Sqrt(2)},
            {-1, 0},
            {-1/Mathf.Sqrt(2), -1/Mathf.Sqrt(2)},
            {0, -1},
        };

        return direction;
    }

    public void SetFire(int x, int y)
    {
        this.grid[x][y].SetState(CellState.on_fire);
        this.hasIgnitedCell = true;
    }

    public float Slope(Cell source, Cell cell)
    {
        float slope = Mathf.Atan((cell.elevation - source.elevation) / 30f) * 180f / Mathf.PI;
        return slope;
    }

    public float SlopeFactor(float slope)
    {
        if (slope < 0f)
            return Mathf.Exp(-0.069f * slope) / (2f * Mathf.Exp(-0.069f * slope) - 1f);
        
        else
            return Mathf.Exp(0.069f * slope);
    }

    public float Ratio(Cell source, Cell cell, float[] dir)
    {
        return FireSimulator.R_0 * this.SlopeFactor(this.Slope(source, cell));
    }

    public void Update()
    {
        for (int x = 0; x < this.grid.Count; x++)
        {
            for (int y = 0; y < this.grid[0].Count; y++)
            {
                this.grid[x][y].SetState(this.new_grid[x][y].state);
            }
        }
    }

    public void Simulate()
    {
        for (int x = 1; x < this.grid.Count-1; x++)
        {
            for (int y = 1; y < this.grid[0].Count-1; y++)
            {
                this.new_grid[x][y].UpdateCell();

                if (this.grid[x][y].state == CellState.on_fire)
                {
                    List<Tuple<Cell, float[]>> tuples = CellGrid.ToTuples(Neighbours(x, y), Direction());

                    List<Tuple<Cell, float[]>> filtered_tuples = new List<Tuple<Cell, float[]>>();
                    foreach (var t in tuples)
                    {
                        if (t.Item1.state == CellState.burnable)
                            filtered_tuples.Add(t);
                    }

                    foreach (var t in filtered_tuples)
                    {
                        if (this.Ratio(this.grid[x][y], t.Item1, t.Item2) > FireSimulator.thresh_up)
                        {
                            t.Item1.SetState(CellState.on_fire);
                        }
                        
                        else if (this.Ratio(this.grid[x][y], t.Item1, t.Item2) > FireSimulator.thresh_down)
                        {
                            t.Item1.SetState(CellState.ignited);
                        }
                    }
                }
            }
        }

        this.Update();
    }

    public static List<Tuple<Cell, float[]>> ToTuples(List<Cell> cells, float[,] dirs)
    {
        List<Tuple<Cell, float[]>> tuples = new List<Tuple<Cell, float[]>>();

        for (int i = 0; i < cells.Count; i++)
        {
            Tuple<Cell, float[]> t = Tuple.Create(cells[i], new float[] { dirs[i, 0], dirs[i, 1] });
            tuples.Add(t);
        }

        return tuples;
    }
}