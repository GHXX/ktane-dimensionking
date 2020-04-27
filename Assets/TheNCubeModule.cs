using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TheNCube;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The NCube
/// Based on the Hyper and Ultracube created by Timwi
/// </summary>
public class TheNCubeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMRuleSeedable RuleSeedable;
    public KMAudio Audio;
    public Transform Ultracube;
    public Transform[] Edges;
    public KMSelectable[] Vertices;
    public MeshFilter[] Faces;
    public Mesh Quad;
    public Material FaceMaterial;

    public Shader DiffuseVertexShader; // KT/Mobile/DiffuseTint
    public Shader TransparentVertexShader; // KT/Transparent/Mobile Diffuse Underlay200

    private VertexSelectableNDCompound[] translationCompounds = new VertexSelectableNDCompound[0];

    // Rule-seed
    private int[][] _colorPermutations;
    private List<bool?[]> _faces;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private int[] _rotations;
    private float _hue, _sat, _v;
    private Coroutine _rotationCoroutine;
    private bool _transitioning;
    private int _progress;
    private int[] _vertexColors;
    private int _correctVertex;
    private string[] _rotationNames = new string[0];
    private string[] _shapeNames = new string[0];

    private Material _edgesMat, _verticesMat, _facesMat;
    private readonly List<Mesh> _generatedMeshes = new List<Mesh>();
    private static readonly char[] _axesNames = "XYZWVUTSRQPONMLKJIHGFEDCBA".ToCharArray();

    private static readonly string[][] _dimensionNames = new[] { 
                                    // dim|Axis
        new[] { "left", "right" },  // 1 = X
        new[] { "bottom", "top" },  // 2 = Y
        new[] { "front", "back" },  // 3 = Z
        new[] { "zig", "zag" },     // 4 = W
        new[] { "ping", "pong" },   // 5 = V
        new[] { "tick", "tock" },   // 6 = U
        new[] { "click", "clack" }, // 7 = T
        new[] { "tip", "tap" },     // 8 = S
        new[] { "this", "that" },   // 9 = R
        new[] { "ying", "yang" }    //10 = Q
    };
    private static readonly string[] _colorNames = new[] { "red", "yellow", "green", "blue" };
    private static readonly Color[] _vertexColorValues = "e54747,e5e347,47e547,3ba0f1".Split(',').Select(str => new Color(Convert.ToInt32(str.Substring(0, 2), 16) / 255f, Convert.ToInt32(str.Substring(2, 2), 16) / 255f, Convert.ToInt32(str.Substring(4, 2), 16) / 255f)).ToArray();
    private int[] _shapeOrder = { 4, 3, 1, 2, 0 };
    private int dimensionCount = -1; // from 6 to 9 (inclusive)
    private bool hideFaces = true;

    private string GetCurrentAxesChars() { return _axesNames.Take(this.dimensionCount).Join(""); }

    private int GetVertexAndOtherCount(int dimensionCount_n, int faceDimension_m) // m == 0 = vertex // m == 1 = edge // m == 2 = face ...
    {
        if (dimensionCount_n == 0 && faceDimension_m == 0)
            return 1;

        if (dimensionCount_n < faceDimension_m || dimensionCount_n < 0 || faceDimension_m < 0)
            return 0;

        return 2 * GetVertexAndOtherCount(dimensionCount_n - 1, faceDimension_m) + GetVertexAndOtherCount(dimensionCount_n - 1, faceDimension_m - 1);
    }

    void Start()
    {
        this._moduleId = _moduleIdCounter++;
        this.dimensionCount = 9;

        // generate shape names
        var predefinedNames = new[] { "vertex", "edge", "face", "cube", "hypercube", "ultracube" };
        this._shapeNames = new string[this.dimensionCount + 1];

        for (int i = 0; i <= this.dimensionCount; i++)
        {
            if (i < predefinedNames.Length)
                this._shapeNames[i] = predefinedNames[i];
            else
                this._shapeNames[i] = i + "-cube";
        }


        // setup rotation names like this (for XYZW):
        //"XY", "YX",
        //"XZ", "ZX",
        //"XW", "WX",
        //"XV", "VX",
        //"YZ", "ZY", 
        //"YW", "WY", 
        //"YV", "VY",
        //"ZW", "WZ",
        //"ZV", "VZ",
        //"WV", "VW"

        var tempRotationNames = new List<string>();
        for (int i = 0; i < this.dimensionCount; i++)
        {
            var firstChar = _axesNames[i];
            for (int j = i + 1; j < this.dimensionCount; j++)
            {
                var secondChar = _axesNames[j];
                tempRotationNames.Add(string.Concat(firstChar, secondChar));
                tempRotationNames.Add(string.Concat(secondChar, firstChar));
            }
        }
        this._rotationNames = tempRotationNames.ToArray();

        // calculate vertex-, edge- and face-counts
        var vertexCount = (int)Math.Pow(2, this.dimensionCount);
        var edgeCount = GetVertexAndOtherCount(this.dimensionCount, 1);
        var faceCount = GetVertexAndOtherCount(this.dimensionCount, 2);

        if (this.Vertices.Count() > vertexCount)
        {
            for (int i = vertexCount; i < this.Vertices.Count(); i++)
            {
                DestroyImmediate(this.Vertices[i].gameObject);
            }
            this.Vertices = this.Vertices.Take(vertexCount).ToArray();
        }

        if (this.Faces.Count() > faceCount)
        {
            for (int i = faceCount; i < this.Faces.Count(); i++)
            {
                DestroyImmediate(this.Faces[i].gameObject);
            }
            this.Faces = this.Faces.Take(faceCount).ToArray();
        }

        if (this.Edges.Count() > edgeCount)
        {
            for (int i = edgeCount; i < this.Edges.Count(); i++)
            {
                DestroyImmediate(this.Edges[i].gameObject);
            }
            this.Edges = this.Edges.Take(edgeCount).ToArray();
        }

        // setup 2^dimensionCount vertices
        var existingVertices = new List<KMSelectable>(this.Vertices);
        var verticesToReParent = new List<KMSelectable>();
        for (int i = 0; i < (vertexCount - this.Vertices.Count()); i++)
        {
            var newVertexGo = Instantiate(existingVertices[0].gameObject);
            var newVertex = newVertexGo.GetComponent<KMSelectable>();
            newVertex.transform.parent = existingVertices[0].transform.parent;
            newVertex.transform.localScale = existingVertices[0].transform.localScale;
            newVertex.name = "newVertex " + (this.Vertices.Count() + i + 1);
            existingVertices.Add(newVertex);
            verticesToReParent.Add(newVertex);
        }

        this.Vertices = existingVertices.ToArray();
        existingVertices[0].Parent.ChildRowLength = vertexCount;
        existingVertices[0].Parent.Children = this.Vertices;
        existingVertices[0].Parent.UpdateChildren();
        existingVertices.Clear();



        // setup edgeCount edges
        var existingEdges = new List<Transform>(this.Edges);
        for (int i = 0; i < (edgeCount - this.Edges.Count()); i++)
        {
            var newEdge = Instantiate(existingEdges[0]);
            newEdge.name = "Edge " + (this.Edges.Count() + i + 1);
            newEdge.transform.parent = existingEdges[0].transform.parent;
            existingEdges.Add(newEdge);
        }
        this.Edges = existingEdges.ToArray();
        existingEdges.Clear();

        // setup faceCount faces
        var existingFaces = new List<MeshFilter>(this.Faces);
        for (int i = 0; i < (faceCount - this.Faces.Count()); i++)
        {
            var newFace = Instantiate(existingFaces[0]);
            newFace.name = "Face " + (this.Faces.Count() + i + 1);
            newFace.transform.parent = existingFaces[0].transform.parent;
            newFace.transform.localScale = existingFaces[0].transform.localScale;
            newFace.transform.position = existingFaces[0].transform.position;
            existingFaces.Add(newFace);
        }
        this.Faces = existingFaces.ToArray();
        existingFaces.Clear();

        // setup materials
        this._edgesMat = this.Edges[0].GetComponent<MeshRenderer>().material;
        for (int i = 0; i < this.Edges.Length; i++)
            this.Edges[i].GetComponent<MeshRenderer>().sharedMaterial = this._edgesMat;

        this._verticesMat = this.Vertices[0].GetComponent<MeshRenderer>().material;
        for (int i = 0; i < this.Vertices.Length; i++)
            this.Vertices[i].GetComponent<MeshRenderer>().sharedMaterial = this._verticesMat;

        this._facesMat = this.Faces[0].GetComponent<MeshRenderer>().material;
        for (int i = 0; i < this.Faces.Length; i++)
            this.Faces[i].GetComponent<MeshRenderer>().sharedMaterial = this._facesMat;

        SetNCube(GetUnrotatedVertices());



        // RULE SEED
        var rnd = this.RuleSeedable.GetRNG();
        Debug.LogFormat("[The NCube #{0}] Using rule seed: {1}", this._moduleId, rnd.Seed);
        this._faces = new List<bool?[]>();

        if (this.dimensionCount < this._shapeOrder.Length)
        {
            this._shapeOrder = this._shapeOrder.TakeLast(this.dimensionCount).ToArray();
        }

        if (this.dimensionCount > this._shapeOrder.Length)
        {
            var tmpShapeOrder = Enumerable.Range(0, this.dimensionCount).Reverse().ToArray();
            this._shapeOrder.CopyTo(tmpShapeOrder, this.dimensionCount - this._shapeOrder.Length);
            this._shapeOrder = tmpShapeOrder;
        }

        SetupFacesRecursively(rnd);

        //for (var i = 0; i < _shapeOrder.Length; i++)
        //    for (var j = i + 1; j < _shapeOrder.Length; j++)
        //        for (var k = j + 1; k < _shapeOrder.Length; k++)
        //        {
        //            var which1 = rnd.Next(0, 2) != 0;
        //            var which2 = rnd.Next(0, 2) != 0;
        //            switch (rnd.Next(0, 3))
        //            {
        //                case 0:
        //                    this._faces.Add(Enumerable.Range(0, 5).Select(d => d == _shapeOrder[i] ? false : d == _shapeOrder[j] ? which1 : d == _shapeOrder[k] ? which2 : (bool?)null).ToArray());
        //                    this._faces.Add(Enumerable.Range(0, 5).Select(d => d == _shapeOrder[i] ? true : d == _shapeOrder[j] ? which1 : d == _shapeOrder[k] ? which2 : (bool?)null).ToArray());
        //                    break;
        //                case 1:
        //                    this._faces.Add(Enumerable.Range(0, 5).Select(d => d == _shapeOrder[i] ? which1 : d == _shapeOrder[j] ? false : d == _shapeOrder[k] ? which2 : (bool?)null).ToArray());
        //                    this._faces.Add(Enumerable.Range(0, 5).Select(d => d == _shapeOrder[i] ? which1 : d == _shapeOrder[j] ? true : d == _shapeOrder[k] ? which2 : (bool?)null).ToArray());
        //                    break;
        //                default:
        //                    this._faces.Add(Enumerable.Range(0, 5).Select(d => d == _shapeOrder[i] ? which1 : d == _shapeOrder[j] ? which2 : d == _shapeOrder[k] ? false : (bool?)null).ToArray());
        //                    this._faces.Add(Enumerable.Range(0, 5).Select(d => d == _shapeOrder[i] ? which1 : d == _shapeOrder[j] ? which2 : d == _shapeOrder[k] ? true : (bool?)null).ToArray());
        //                    break;
        //            }
        //        }

        var res = this._faces.Select(a => string.Join(" ", a.Select(x => x.HasValue ? x.ToString() : "null").ToArray())).ToArray();
        var resStr = string.Join("\n", res);

        rnd.ShuffleFisherYates(this._faces);

        var colors = new[] { "RYGB", "RYBG", "RGYB", "RGBY", "RBYG", "RBGY", "YRGB", "YRBG", "YGRB", "YGBR", "YBRG", "YBGR", "GRYB", "GRBY", "GYRB", "GYBR", "GBRY", "GBYR", "BRYG", "BRGY", "BYRG", "BYGR", "BGRY", "BGYR" }
                .Select(str => str.Select(ch => "RYGB".IndexOf(ch)).ToArray()).ToList();

        var colorResult = new List<int[]>(colors);

        int neededColors = Enumerable.Range(0, this.dimensionCount).Sum() * 2;
        int multiplier = 0;
        while (colorResult.Count < neededColors)
        {
            colorResult.AddRange(colors);
            multiplier++;
        }
        this._colorPermutations = rnd.ShuffleFisherYates(colorResult.Take(neededColors).ToArray()).ToArray();
        Debug.LogFormat("[The NCube #{0}] Generated {1} color permutations taken from a set of {2} colors, multiplied by {3}.", this._moduleId, colorResult.Count, colors.Count, multiplier);


        Debug.LogFormat("[The NCube #{0}] Rules:\n{1}", this._moduleId, Enumerable.Range(0, this._rotationNames.Length).Select(ix => string.Format("{0}={1}", this._rotationNames[ix], StringifyShape(this._faces[ix]))).Join("\n"));

        // GENERATE PUZZLE
        this._rotations = new int[5];
        for (int i = 0; i < this._rotations.Length; i++)
        {
            var axes = GetCurrentAxesChars().ToArray().Shuffle();
            this._rotations[i] = Array.IndexOf(this._rotationNames, string.Concat(axes[0], axes[1]));
        }

        // ## FOR CREATING THE “ALL ROTATIONS” GIF
        //if (_moduleId >= 1 && _moduleId <= 20)
        //{
        //    _rotations = new int[5] { _moduleId - 1, _moduleId - 1, _moduleId - 1, _moduleId - 1, _moduleId - 1 };
        //    RotationText.text = _rotationNames[_moduleId - 1];
        //    RotationText.gameObject.SetActive(true);
        //}
        //else
        //    RotationText.gameObject.SetActive(false);
        // ## END

        Debug.LogFormat(@"[The NCube #{0}] Rotations are: {1}", this._moduleId, this._rotations.Select(rot => this._rotationNames[rot]).Join(", "));

        for (var i = 0; i < this.Vertices.Count(); i++)
            this.Vertices[i].OnInteract = VertexClick(i);

        this._rotationCoroutine = StartCoroutine(RotateUltracube());
    }

    private void SetupFacesRecursively(MonoRandom rnd, int depth = 1, params int[] forloopvars) // forloopvars contains the variables dimSkip,a,b,c... of the recursive forloops
    {
        if (depth == 1)
        {
            this._faces = new List<bool?[]>();

            for (int dimskip = 0; dimskip < this.dimensionCount; dimskip++)
            {
                var newForloopvars = new int[] { dimskip };
                SetupFacesRecursively(rnd, depth + 1, newForloopvars);
            }
        }
        else if (depth < this.dimensionCount)
        {
            for (int i = 0; i < 2; i++) // loop true, false
            {
                var newForloopvars = new int[forloopvars.Length + 1];
                Array.Copy(forloopvars, newForloopvars, forloopvars.Length);
                newForloopvars[forloopvars.Length] = i;
                SetupFacesRecursively(rnd, depth + 1, newForloopvars);
            }
        }
        else
        {
            var dimskip = forloopvars[0]; // this and the next dimension are set to null

            // var axesTrueFalse = forloopvars[1,2,3,4, ...]
            var arr = new bool?[this.dimensionCount];


            for (int valueIndex = 0; valueIndex < this.dimensionCount; valueIndex++)
            {
                int actualindex = (valueIndex + dimskip) % arr.Length;
                if (valueIndex < 2) // if 0 or 1 set it to null
                {
                    arr[actualindex] = null;
                }
                else // else, grab the values for the x y z ... axes
                {
                    arr[actualindex] = forloopvars[valueIndex - 1] == 1;  // and if its 1 then set it to true, otherwise false
                }
            }

            this._faces.Add(arr);
        }
    }

    private PointND[] GetUnrotatedVertices()
    {
        return Enumerable.Range(0, 1 << this.dimensionCount).Select(i => new PointND(Enumerable.Range(0, this.dimensionCount).Select(x => (i & (1 << x)) != 0 ? 1d : -1d).ToArray())).ToArray();
    }

    private KMSelectable.OnInteractHandler VertexClick(int v)
    {
        return delegate
        {
            this.Vertices[v].AddInteractionPunch(.2f);
            if (this._transitioning)
                return false;

            if (this._rotationCoroutine != null)
            {
                this._progress = 0;
                StartCoroutine(ColorChange(setVertexColors: true));
            }
            else if (this.Vertices[v] != this.selectedVertexSelectable)
            {
                SetAndUpdateVertexVisibility(this.Vertices[v]);
            }
            else if (v == this._correctVertex)
            {
                SetAndUpdateVertexVisibility(null);
                this._progress++;
                if (this._progress == 4)
                {
                    Debug.LogFormat(@"[The NCube #{0}] Module solved.", this._moduleId);
                    this.Module.HandlePass();
                    StartCoroutine(ColorChange(keepGrey: true));
                    this.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, this.transform);
                }
                else
                {
                    StartCoroutine(ColorChange(setVertexColors: true));
                }
            }
            else
            {
                Debug.LogFormat(@"[The NCube #{0}] Incorrect vertex {1} pressed; resuming rotations.", this._moduleId, StringifyShape(v));
                this.Module.HandleStrike();
                this._rotationCoroutine = StartCoroutine(RotateUltracube(delay: true));
            }
            return false;
        };
    }

    private string StringifyShape(bool?[] shape)
    {
        var strs = this._shapeOrder.Select(d => shape[d] == null ? null : _dimensionNames[d][shape[d].Value ? 1 : 0]).Where(s => s != null).ToArray();
        //return strs.Length == 0
        //    ? "ultracube"
        //    : strs.Join("-") + " " + (
        //        strs.Length == 1 ? "hypercube" :
        //        strs.Length == 2 ? "cube" :
        //        strs.Length == 3 ? "face" :
        //        strs.Length == 4 ? "edge" : "vertex");
        var index = this.dimensionCount - strs.Length;

        if (strs.Length == 0)
        {
            return this._shapeNames[index];
        }

        return strs.Join("-") + " " + this._shapeNames[index];
    }
    private string StringifyShape(int vertex)
    {
        return StringifyShape(Enumerable.Range(0, this.dimensionCount).Select(d => (bool?)((vertex & (1 << d)) != 0)).ToArray());
    }

    private IEnumerator ColorChange(bool keepGrey = false, bool setVertexColors = false, bool delay = false, bool skipGrey = false)
    {
        this._transitioning = true;
        for (int i = 0; i < this.Vertices.Length; i++)
            this.Vertices[i].GetComponent<MeshRenderer>().sharedMaterial = this._verticesMat;

        var prevHue = .5f;
        var prevSat = 0f;
        var prevV = .5f;
        if (skipGrey)
        {
            Color.RGBToHSV(this._edgesMat.color, out prevHue, out prevSat, out prevV);
        }
        else
        {
            SetColor(prevHue, prevSat, prevV);
        }

        if (keepGrey)
            yield break;

        yield return new WaitForSeconds(delay ? 2.22f : .22f);

        this._hue = Rnd.Range(0f, 1f);
        this._sat = Rnd.Range(.6f, .9f);
        this._v = Rnd.Range(.75f, 1f);

        var duration = 1.5f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SetColor(Mathf.Lerp(prevHue, this._hue, elapsed / duration), Mathf.Lerp(prevSat, this._sat, elapsed / duration), Mathf.Lerp(prevV, this._v, elapsed / duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        SetColor(this._hue, this._sat, this._v);

        if (setVertexColors)
        {
            yield return new WaitUntil(() => this._rotationCoroutine == null);
            PlayRandomSound();

            var desiredFace = this._faces[this._rotations[this._progress]];
            var initialColors = Enumerable.Range(0, 4).ToList();
            var q = new Queue<int>();
            var colors = new int?[1 << this.dimensionCount];
            // TODO are all vertices clickable?
            Debug.LogFormat(@"[The NCube #{0}] Stage {1} correct face: {2}", this._moduleId, this._progress + 1, StringifyShape(desiredFace));
            Debug.LogFormat(@"[The NCube #{0}] Stage {1} correct color: {2}", this._moduleId, this._progress + 1, _colorNames[this._colorPermutations[this._rotations[4]][this._progress]]);

            // Assign the four colors on the desired face
            for (int v = 0; v < 1 << this.dimensionCount; v++)
            {
                if (Enumerable.Range(0, this.dimensionCount).All(d => desiredFace[d] == null || ((v & (1 << d)) != 0) == desiredFace[d].Value))
                {
                    var ix = Rnd.Range(0, initialColors.Count);
                    colors[v] = initialColors[ix];
                    initialColors.RemoveAt(ix);
                    for (var d = 0; d < this.dimensionCount; d++)
                        q.Enqueue(v ^ (1 << d));

                    if (colors[v].Value == this._colorPermutations[this._rotations[4]][this._progress])
                    {
                        this._correctVertex = v;
                        Debug.LogFormat(@"[The NCube #{0}] Stage {1} correct vertex: {2}", this._moduleId, this._progress + 1, StringifyShape(this._correctVertex));
                    }
                }
            }

            // Assign the remaining colors as best as possible
            while (q.Count > 0)
            {
                var vx = q.Dequeue();
                if (colors[vx] != null)
                    continue;

                // For each color, determine how many faces would have a clash
                var numClashesPerColor = new int[4];
                for (var color = 0; color < 4; color++)
                    for (var d = 0; d < this.dimensionCount; d++)
                        for (var e = d + 1; e < this.dimensionCount; e++)
                            if (Enumerable.Range(0, 1 << this.dimensionCount).Any(v => (v & (1 << d)) == (vx & (1 << d)) && (v & (1 << e)) == (vx & (1 << e)) && colors[v] == color))
                                numClashesPerColor[color]++;

                var cs = Enumerable.Range(0, 4).ToArray();
                Array.Sort(numClashesPerColor, cs);

                //int[] csCanditates = new int[4];
                //int len = 1;
                //csCanditates[0] = cs[0];

                //for (int i = 1; i < 4; i++)
                //{
                //    if (cs[i] == cs[0])
                //    {
                //        csCanditates[i] = cs[i];
                //        len++;
                //    }
                //}
                //if (len == 1)
                //    colors[vx] = cs[0];
                //else
                //    colors[vx] = cs[Rnd.Range(0, len)];
                colors[vx] = cs.PickRandom(); // TODO make the best assignment thing work again
                cs = Enumerable.Range(0, this.dimensionCount).ToArray().Shuffle();
                for (var d = 0; d < this.dimensionCount; d++)
                    q.Enqueue(vx ^ (1 << cs[d]));
            }
            int unassigned = colors.Count(x => x.Value == 0);
            int cnt = colors.Length;
            double pct = unassigned * 100d / cnt;
            this._vertexColors = colors.Select(v => v.Value).ToArray();
            for (int v = 0; v < 1 << this.dimensionCount; v++)
            {
                var c = _vertexColorValues[this._vertexColors[v]];
                c.a = 0.2f;
                this.Vertices[v].GetComponent<MeshRenderer>().material.color = c;
            }
        }

        var ec = this.Edges[0].GetComponent<MeshRenderer>().material.color;
        ec.a = 0f;
        for (int e = 0; e < this.Edges.Length; e++)
        {
            this.Edges[e].GetComponent<MeshRenderer>().material.color = ec;
        }

        this._transitioning = false;
    }

    private bool?[] selectedVertex = null;
    private KMSelectable selectedVertexSelectable = null;

    private void SetAndUpdateVertexVisibility(KMSelectable newVertexSelection)
    {
        if (newVertexSelection == null)
        {
            this.selectedVertex = null;
            this.selectedVertexSelectable = null;
        }
        else
        {
            var vertexCompound = this.translationCompounds.First(x => x.selectable == newVertexSelection);

            this.selectedVertexSelectable = newVertexSelection;
            this.selectedVertex = vertexCompound.vectorN.Coordinates.Select(x => (bool?)(x > 0)).ToArray();
        }

        UpdateVertexVisibility();
    }

    private void UpdateVertexVisibility()
    {
        if (this.selectedVertex == null)
        {
            for (int v = 0; v < 1 << this.dimensionCount; v++)
            {
                var c = _vertexColorValues[this._vertexColors[v]];
                c.a = 1f;
                var mat = this.Vertices[v].GetComponent<MeshRenderer>().material;
                mat.color = c;
                mat.shader = this.DiffuseVertexShader;
            }
        }
        else
        {
            double alphaMultiplicator = 0.5d;
            for (int v = 0; v < 1 << this.dimensionCount; v++)
            {
                var c = _vertexColorValues[this._vertexColors[v]];
                var currentVertex = this.translationCompounds.First(x => x.selectable == this.Vertices[v]).vectorN.Coordinates.Select(x => (bool?)(x > 0)).ToArray();

                c.a = (float)Math.Pow(alphaMultiplicator, GetDistanceBetweenVertices(this.selectedVertex, currentVertex));
                var mat = this.Vertices[v].GetComponent<MeshRenderer>().material;
                mat.color = c;
                mat.shader = this.TransparentVertexShader;
            }
        }
    }

    private int GetDistanceBetweenVertices(bool?[] vertex1, bool?[] vertex2)
    {
        if (vertex1.Length != vertex2.Length)
            throw new ArgumentException("vertex 1 and vertex 2 got a different number of dimensions!");

        int cnt = 0;
        for (int i = 0; i < vertex1.Length; i++)
        {
            if (!vertex1[i].HasValue)
                throw new ArgumentNullException("An axis of vertex1 was null!");

            if (!vertex2[i].HasValue)
                throw new ArgumentNullException("An axis of vertex2 was null!");

            if (vertex1[i].Value != vertex2[i].Value)
                cnt++;
        }
        return cnt;
    }

    private List<bool?[]> GetNeighbourVertices(bool?[] vertex)
    {
        if (vertex.Any(x => !x.HasValue))
        {
            throw new ArgumentNullException("A vertex that was passed had null instead of true or false.");
        }

        var retVal = new List<bool?[]>();
        for (int i = 0; i < this.dimensionCount; i++)
        {
            var copy = new bool?[vertex.Length];
            Array.Copy(vertex, 0, copy, 0, vertex.Length);
            copy[i] = !copy[i].Value;

            retVal.Add(copy);
        }

        return retVal;
    }

    private void PlayRandomSound()
    {
        this.Audio.PlaySoundAtTransform("Bleep" + Rnd.Range(1, 11), this.transform);
    }

    private void SetColor(float h, float s, float v)
    {
        this._edgesMat.color = Color.HSVToRGB(h, s, v);
        this._verticesMat.color = Color.HSVToRGB(h, s * .8f, v * .5f);
        var clr = Color.HSVToRGB(h, s * .8f, v * .75f);
        clr.a = .1f;
        this._facesMat.color = clr;
    }

    private IEnumerator RotateUltracube(bool delay = false)
    {
        var colorChange = ColorChange(delay: delay);
        while (colorChange.MoveNext())
            yield return colorChange.Current;

        var unrotatedVertices = GetUnrotatedVertices();
        SetNCube(unrotatedVertices);

        while (!this._transitioning)
        {
            yield return new WaitForSeconds(Rnd.Range(1.75f, 2.25f));

            for (int rot = 0; rot < this._rotations.Length && !this._transitioning; rot++)
            {
                var axis1 = GetCurrentAxesChars().IndexOf(this._rotationNames[this._rotations[rot]][0]);
                var axis2 = GetCurrentAxesChars().IndexOf(this._rotationNames[this._rotations[rot]][1]);
                var duration = 2f;
                var elapsed = 0f;

                while (elapsed < duration)
                {
                    var angle = EaseInOutQuad(elapsed, 0, Mathf.PI / 2, duration);
                    var matrix = new double[this.dimensionCount * this.dimensionCount];
                    for (int i = 0; i < this.dimensionCount; i++)
                        for (int j = 0; j < this.dimensionCount; j++)
                            matrix[i + this.dimensionCount * j] =
                                i == axis1 && j == axis1 ? Mathf.Cos(angle) :
                                i == axis1 && j == axis2 ? Mathf.Sin(angle) :
                                i == axis2 && j == axis1 ? -Mathf.Sin(angle) :
                                i == axis2 && j == axis2 ? Mathf.Cos(angle) :
                                i == j ? 1 : 0;

                    SetNCube(unrotatedVertices.Select(v => (v * matrix)).ToArray());

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                // Reset the position of the NCube
                SetNCube(unrotatedVertices);
                yield return new WaitForSeconds(Rnd.Range(.5f, .6f));
            }


            //var colorChange2 = ColorChange(delay: true, skipGrey: true);
            //while (colorChange2.MoveNext())
            //    yield return colorChange2.Current;
        }

        this._transitioning = false;
        this._rotationCoroutine = null;
    }

    private static float EaseInOutQuad(float t, float start, float end, float duration)
    {
        var change = end - start;
        t /= duration / 2;
        if (t < 1)
            return change / 2 * t * t + start;
        t--;
        return -change / 2 * (t * (t - 2) - 1) + start;
    }

    private void SetNCube(PointND[] verticesNd)
    {
        bool overwrite = false;
        if (verticesNd.Length != this.translationCompounds.Length)
        {
            overwrite = true;
        }
        var newTransCompounds = new List<VertexSelectableNDCompound>();
        var vertices3d = verticesNd.Select(x => x.Project()).ToArray();

        // VERTICES
        for (int i = 0; i < 1 << this.dimensionCount; i++)
        {
            if (overwrite)
            {
                newTransCompounds.Add(new VertexSelectableNDCompound(this.Vertices[i], verticesNd[i]));
            }
            this.Vertices[i].transform.localPosition = vertices3d[i];
        }

        if (overwrite)
            this.translationCompounds = newTransCompounds.ToArray();

        // EDGES
        var e = 0;
        for (int i = 0; i < 1 << this.dimensionCount; i++)
            for (int j = i + 1; j < 1 << this.dimensionCount; j++)
                if (((i ^ j) & ((i ^ j) - 1)) == 0)
                {
                    this.Edges[e].localPosition = (vertices3d[i] + vertices3d[j]) / 2;
                    this.Edges[e].localRotation = Quaternion.FromToRotation(Vector3.up, vertices3d[j] - vertices3d[i]);
                    this.Edges[e].localScale = new Vector3(.1f, (vertices3d[j] - vertices3d[i]).magnitude / 2, .1f);
                    e++;
                }

        // FACES
        foreach (var mesh in this._generatedMeshes)
            Destroy(mesh);
        this._generatedMeshes.Clear();

        if (!this.hideFaces)
        {
            var f = 0;
            for (int i = 0; i < 1 << this.dimensionCount; i++)
                for (int j = i + 1; j < 1 << this.dimensionCount; j++)
                {
                    var b1 = i ^ j;
                    var b2 = b1 & (b1 - 1);
                    if (b2 != 0 && (b2 & (b2 - 1)) == 0 && (i & b1 & ((i & b1) - 1)) == 0 && (j & b1 & ((j & b1) - 1)) == 0)
                    {
                        var mesh = new Mesh { vertices = new[] { vertices3d[i], vertices3d[i | j], vertices3d[i & j], vertices3d[j] }, triangles = new[] { 0, 1, 2, 1, 2, 3, 2, 1, 0, 3, 2, 1 } };
                        mesh.RecalculateNormals();
                        this._generatedMeshes.Add(mesh);
                        this.Faces[f].sharedMesh = mesh;
                        f++;
                    }
                }
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} go [use when ultracube is rotating] | !{0} pong-zig-bottom-front-left [presses a vertex when the ncube is not rotating]";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (this._rotationCoroutine != null && Regex.IsMatch(command, @"^\s*(go|activate|stop|run|start|on|off)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            yield return new[] { this.Vertices[0] };
            yield break;
        }

        Match m;
        if (this._rotationCoroutine == null && (m = Regex.Match(command, string.Format(@"^\s*((?:{0})(?:[- ,;]*(?:{0}))*)\s*$", _dimensionNames.SelectMany(x => x).Join("|")), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var elements = m.Groups[1].Value.Split(new[] { ' ', ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (elements.Length != 5)
            {
                yield return "sendtochaterror Dude, it’s a 5D ultracube, you gotta have 5 dimensions.";
                yield break;
            }
            var dimensions = elements.Select(el => _dimensionNames.ToList().FindIndex(d => d.Any(dn => dn.EqualsIgnoreCase(el)))).ToArray();
            var invalid = Enumerable.Range(0, 4).SelectMany(i => Enumerable.Range(i + 1, 4 - i).Where(j => dimensions[i] == dimensions[j]).Select(j => new { i, j })).FirstOrDefault();
            if (invalid != null)
            {
                yield return elements[invalid.i].EqualsIgnoreCase(elements[invalid.j])
                    ? string.Format("sendtochaterror Dude, you wrote “{0}” twice.", elements[invalid.i], elements[invalid.j])
                    : string.Format("sendtochaterror Dude, “{0}” and “{1}” doesn’t jive.", elements[invalid.i], elements[invalid.j]);
                yield break;
            }
            var vertexIx = 0;
            for (int i = 0; i < 5; i++)
                vertexIx |= _dimensionNames[dimensions[i]].ToList().FindIndex(dn => dn.EqualsIgnoreCase(elements[i])) << dimensions[i];
            yield return null;
            yield return new[] { this.Vertices[vertexIx] };
        }
    }
}
