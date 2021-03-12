using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Linq;

public class EnvironmentManager : MonoBehaviour
{
    #region Fields and properties

    VoxelGrid _voxelGrid;
    int _randomSeed = 666;

    bool _showVoids = true;
    Pix2Pix _pix2pix;

    #endregion

    #region Unity Standard Methods

    void Start()
    {
        // Initialise the voxel grid
        Vector3Int gridSize = new Vector3Int(64, 64, 64);
        _voxelGrid = new VoxelGrid(gridSize, Vector3.zero, 1, parent: this.transform);

        // Set the random engine's seed
        Random.InitState(_randomSeed);
        _pix2pix = new Pix2Pix();
    }

    void Update()
    {
        // Draw the voxels according to their Function Colors
        DrawVoxels();

        // Use the V key to switch between showing voids
        if (Input.GetKeyDown(KeyCode.V))
        {
            _showVoids = !_showVoids;
        }
        if (Input.GetMouseButton(0))
        {
            var voxel = SelectVoxel();
            if (voxel != null)
            {
                print(voxel.Index);
                //_voxelGrid.CreateBlackBlob(voxel.Index, 15, picky: true, flat: false); //Int is the radius of neighbours
                _voxelGrid.CreateBlackRectangle(voxel.Index, Random.Range(2, 12), Random.Range(4, 20), Random.Range(10, 15));
                //PredictAndUpdate(allLayers: true);
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            _voxelGrid.ClearGrid();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            //CreateRandomBlobs(4,5,10);
            //PopulateRandomBlobsAndSave(500,3, 4, 3, 32);
            //var gridImage = _voxelGrid.ImageFromGrid();
            //_pix2pix.Predict(_voxelGrid.ImageFromGrid());
            PredictAndUpdate(allLayers: true);

        }

    }

    #endregion

    #region Private Methods
    void PredictAndUpdate(bool allLayers = false)
    {
        _voxelGrid.ClearReds();
        int layerCount = 1;
        if (allLayers)
        {
            layerCount = _voxelGrid.GridSize.y;
        }

        for (int i = 0; i < layerCount; i++)
        {
            var gridImage = _voxelGrid.ImageFromGrid(layer: i);
            var resized = ImageReadWrite.Resize256(gridImage, Color.grey);

            var predicted = _pix2pix.Predict(resized);
            TextureScale.Point(predicted, _voxelGrid.GridSize.x, _voxelGrid.GridSize.z);

            _voxelGrid.SetStatesFromImage(predicted, layer: i);
        }
        //sections along z 
        for (int j = 0; j < _voxelGrid.GridSize.z; j++)
        {
            var gridImage = _voxelGrid.SectionGridXY(section: j);
            var resized = ImageReadWrite.Resize256(gridImage, Color.grey);

            var predicted = _pix2pix.Predict(resized);
            TextureScale.Point(predicted, _voxelGrid.GridSize.x, _voxelGrid.GridSize.y);

            _voxelGrid.SetStatesFromImage(predicted, layer: j);
        }

    }



    void PopulateRandomBlobsAndSave(int sampleSize, int minAmt, int maxAmt, int minRadius, int maxRadius)
    {
        Stopwatch stopwatch = new Stopwatch();

        stopwatch.Start();
        string saveFolder = "Output";
        for (int i = 0; i < sampleSize; i++)
        {
            int amt = Random.Range(minAmt, maxAmt);
            _voxelGrid.ClearGrid();
            CreateRandomBlobs(amt, minRadius, maxRadius, true);

            Texture2D gridImage = _voxelGrid.ImageFromGrid(transparent: true);
            Texture2D resizedImage = ImageReadWrite.Resize256(gridImage, Color.grey);
            ImageReadWrite.SaveImage(resizedImage, $"{saveFolder}/Grid_{i}");
        }
        stopwatch.Stop();

        print($"Took {stopwatch.ElapsedMilliseconds} milliseconds to generate {sampleSize} images");
    }
    
    void CreateRandomBlobs(int amt, int minRadius, int maxRadius, bool picky = true)
    {
        for (int i = 0; i < amt; i++)
        {
            bool success = false;
            while (!success)
            {
                float rand = Random.value;
                int x;
                int z;
                /*
                if(rand < 0.5f)
                {
                    //Condition? value if true : value if false
                    x = Random.value < 0.5f ? 0 : _voxelGrid.GridSize.x - 1; //either min or max
                    z = Random.Range(0, _voxelGrid.GridSize.z);
                }
                else
                {
                    //Condition? value if true : value if false
                    z = Random.value < 0.5f ? 0 : _voxelGrid.GridSize.z - 1; //either min or max
                    x = Random.Range(0, _voxelGrid.GridSize.x);
                }
                */
                //not edees or 50% in edge x & z
                if (rand < 0.5f)
                {
                    //Condition? value if true : value if false
                    x = Random.value < 0.5f ? 0 : Random.Range(0, _voxelGrid.GridSize.x); //either min or max
                    z = Random.Range(0, _voxelGrid.GridSize.z);
                }
                else
                {
                    //Condition? value if true : value if false
                    z = Random.value < 0.5f ? 0 : Random.Range(0, _voxelGrid.GridSize.z); //either min or max
                    x = Random.Range(0, _voxelGrid.GridSize.x);
                }

                Vector3Int origin = new Vector3Int(x, 0, z);
                int radius = Random.Range(minRadius, maxRadius);

                //success = _voxelGrid.CreateBlackBlob(origin, radius, picky);
                success = _voxelGrid.CreateBlackRectangle(origin, Random.Range(minRadius, maxRadius), Random.Range(minRadius, maxRadius), 1);
            }
        }
    }
    Voxel SelectVoxel()
    {
        Voxel selected = null;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Transform objectHit = hit.transform;
            if (objectHit.CompareTag("Voxel"))
            {
                string voxelName = objectHit.name;
                var index = voxelName.Split('_').Select(v => int.Parse(v)).ToArray(); //Extract the integers of the index name of each voxel

                selected = _voxelGrid.Voxels[index[0], index[1], index[2]];
            }
        }
        return selected;
    }

    /// <summary>
    /// Draws the voxels according to it's state and Function Corlor
    /// </summary>
    void DrawVoxels()
    {
        foreach (var voxel in _voxelGrid.Voxels)
        {
            if (voxel.IsActive)
            {
                Vector3 pos = (Vector3)voxel.Index * _voxelGrid.VoxelSize + transform.position;
                if (voxel.FColor    ==   FunctionColor.Black)   Drawing.DrawCube(pos, _voxelGrid.VoxelSize, Color.black);
                else if (voxel.FColor == FunctionColor.Red)     Drawing.DrawCube(pos, _voxelGrid.VoxelSize, Color.red);
                else if (voxel.FColor == FunctionColor.Yellow)  Drawing.DrawCube(pos, _voxelGrid.VoxelSize, Color.yellow);
                else if (voxel.FColor == FunctionColor.Green)   Drawing.DrawCube(pos, _voxelGrid.VoxelSize, Color.green);
                else if (voxel.FColor == FunctionColor.Cyan)    Drawing.DrawCube(pos, _voxelGrid.VoxelSize, Color.cyan);
                else if (voxel.FColor == FunctionColor.Magenta) Drawing.DrawCube(pos, _voxelGrid.VoxelSize, Color.magenta);
                else if (_showVoids && voxel.Index.y == 0)
                    Drawing.DrawTransparentCube(pos, _voxelGrid.VoxelSize);
            }
        }
    }

    #endregion
}
