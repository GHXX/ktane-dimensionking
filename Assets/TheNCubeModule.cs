using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TheUltracube;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The Ultracube
/// Created by Timwi
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
    // dimension number:                                                     1                            2                           3                       4                       5                           6                       7                           8                     9
    private static readonly string[][] _dimensionNames = new[] { new[] { "left", "right" }, new[] { "bottom", "top" }, new[] { "front", "back" }, new[] { "zig", "zag" }, new[] { "ping", "pong" }, new[] { "tick", "tock" }, new[] { "click", "clack" }, new[] { "tip", "tap" }, new[] { "this", "that" } };
    private static readonly string[] _colorNames = new[] { "red", "yellow", "green", "blue" };
    private static readonly Color[] _vertexColorValues = "e54747,e5e347,47e547,3ba0f1".Split(',').Select(str => new Color(Convert.ToInt32(str.Substring(0, 2), 16) / 255f, Convert.ToInt32(str.Substring(2, 2), 16) / 255f, Convert.ToInt32(str.Substring(4, 2), 16) / 255f)).ToArray();
    private int[] _shapeOrder = { 4, 3, 1, 2, 0 };
    private int dimensionCount = -1; // from 6 to 9 (inclusive)

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
        this.dimensionCount = 6;

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
            newEdge.name = "Edge " + (this.Edges.Count()+i + 1);
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
            newFace.name = "Face " + (this.Faces.Count() +i + 1);
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

        SetNCube(GetUnrotatedVertices().Select(p => p.Project()).ToArray());

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

        SetupFacesRecursively(rnd); // TODO fix that code which seems to be responsible for picking faces which need to be clicked later on.

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

    private void SetupFacesRecursively(MonoRandom rnd, int depth = 1, params int[] forloopvars) // forloopvars contains the variables i j k... of the recursive forloops
    {
        if (depth == 1)
        {
            this._faces = new List<bool?[]>();
        }


        if (depth < this.dimensionCount - 1)
        {
            var newForloopvars = new int[forloopvars.Length + 1];
            Array.Copy(forloopvars, newForloopvars, forloopvars.Length);
            int statvalue = forloopvars.Length == 0 ? 0 : forloopvars[forloopvars.Length - 1] + 1;
            for (int i = statvalue; i < this.dimensionCount; i++)
            {
                newForloopvars[forloopvars.Length] = i;
                SetupFacesRecursively(rnd, depth + 1, newForloopvars);
            }
        }
        else
        {
            var faceData = new bool?[this.dimensionCount];
            var faceData2 = new bool?[this.dimensionCount];

            for (int i = 0; i < this.dimensionCount; i++)
            {
                faceData[i] = null;
                faceData2[i] = null;
            }

            var randomVars = new bool[this.dimensionCount - 3].Select(x => rnd.Next(0, 2) != 0).ToArray();
            var truefalseIndex = rnd.Next(0, this.dimensionCount - 2);
            int rngUseIndex = 0;

            for (int d = 0; d < this.dimensionCount; d++)
            {
                for (int forloopvarsindex = 0; forloopvarsindex < forloopvars.Length; forloopvarsindex++)
                {
                    var flvNumber = forloopvars[forloopvarsindex];
                    if (this._shapeOrder[flvNumber] == d)
                    {
                        if (d == 0)
                        {
                            faceData[d] = true;
                            faceData2[d] = false;
                        }
                        else
                        {
                            faceData[d] = randomVars[0];
                            faceData2[d] = randomVars[0];
                        }
                        //if (truefalseIndex == d)
                        //{
                        //    faceData[d] = true;
                        //    faceData2[d] = false;

                        //    forloopvarsindex = forloopvars.Length; // break
                        //}
                        //else
                        //{
                        //    int rngIndex = rngUseIndex;
                        //    if (truefalseIndex < rngIndex)
                        //    {
                        //        rngIndex--;
                        //    }

                        //    faceData[d] = faceData2[d] = randomVars[rngIndex];
                        //    rngUseIndex++;
                        //    forloopvarsindex = forloopvars.Length; // break
                        //}
                    }
                }
            }

            this._faces.Add(faceData);
            this._faces.Add(faceData2);
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
            else if (v == this._correctVertex)
            {
                this._progress++;
                if (this._progress == 4)
                {
                    Debug.LogFormat(@"[The Ultracube #{0}] Module solved.", this._moduleId);
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
                Debug.LogFormat(@"[The Ultracube #{0}] Incorrect vertex {1} pressed; resuming rotations.", this._moduleId, StringifyShape(v));
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
        return StringifyShape(Enumerable.Range(0, 5).Select(d => (bool?)((vertex & (1 << d)) != 0)).ToArray());
    }

    private IEnumerator ColorChange(bool keepGrey = false, bool setVertexColors = false, bool delay = false)
    {
        this._transitioning = true;
        for (int i = 0; i < this.Vertices.Length; i++)
            this.Vertices[i].GetComponent<MeshRenderer>().sharedMaterial = this._verticesMat;

        var prevHue = .5f;
        var prevSat = 0f;
        var prevV = .5f;
        SetColor(prevHue, prevSat, prevV);

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
            var colors = new int?[1 << 5];

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
                    for (var d = 0; d < 5; d++)
                        q.Enqueue(v ^ (1 << d));

                    if (colors[v].Value == this._colorPermutations[this._rotations[4]][this._progress])
                    {
                        this._correctVertex = v;
                        Debug.LogFormat(@"[The Ultracube #{0}] Stage {1} correct vertex: {2}", this._moduleId, this._progress + 1, StringifyShape(this._correctVertex));
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
                colors[vx] = cs[0];

                cs = Enumerable.Range(0, this.dimensionCount).ToArray().Shuffle();
                for (var d = 0; d < this.dimensionCount; d++)
                    q.Enqueue(vx ^ (1 << cs[d]));
            }

            this._vertexColors = colors.Select(v => v.Value).ToArray();
            for (int v = 0; v < 1 << this.dimensionCount; v++)
                this.Vertices[v].GetComponent<MeshRenderer>().material.color = _vertexColorValues[this._vertexColors[v]];
        }

        this._transitioning = false;
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
        SetNCube(unrotatedVertices.Select(v => v.Project()).ToArray());

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

                    SetNCube(unrotatedVertices.Select(v => (v * matrix).Project()).ToArray());

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                // Reset the position of the NCube
                SetNCube(unrotatedVertices.Select(v => v.Project()).ToArray());
                yield return new WaitForSeconds(Rnd.Range(.5f, .6f));
            }
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

    private void SetNCube(Vector3[] vertices)
    {
        // VERTICES
        for (int i = 0; i < 1 << this.dimensionCount; i++)
            this.Vertices[i].transform.localPosition = vertices[i];

        // EDGES
        var e = 0;
        for (int i = 0; i < 1 << this.dimensionCount; i++)
            for (int j = i + 1; j < 1 << this.dimensionCount; j++)
                if (((i ^ j) & ((i ^ j) - 1)) == 0)
                {
                    this.Edges[e].localPosition = (vertices[i] + vertices[j]) / 2;
                    this.Edges[e].localRotation = Quaternion.FromToRotation(Vector3.up, vertices[j] - vertices[i]);
                    this.Edges[e].localScale = new Vector3(.1f, (vertices[j] - vertices[i]).magnitude / 2, .1f);
                    e++;
                }

        // FACES
        foreach (var mesh in this._generatedMeshes)
            Destroy(mesh);
        this._generatedMeshes.Clear();

        var f = 0;
        for (int i = 0; i < 1 << this.dimensionCount; i++)
            for (int j = i + 1; j < 1 << this.dimensionCount; j++)
            {
                var b1 = i ^ j;
                var b2 = b1 & (b1 - 1);
                if (b2 != 0 && (b2 & (b2 - 1)) == 0 && (i & b1 & ((i & b1) - 1)) == 0 && (j & b1 & ((j & b1) - 1)) == 0)
                {
                    var mesh = new Mesh { vertices = new[] { vertices[i], vertices[i | j], vertices[i & j], vertices[j] }, triangles = new[] { 0, 1, 2, 1, 2, 3, 2, 1, 0, 3, 2, 1 } };
                    mesh.RecalculateNormals();
                    this._generatedMeshes.Add(mesh);
                    this.Faces[f].sharedMesh = mesh;
                    f++;
                }
            }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} go [use when ultracube is rotating] | !{0} pong-zig-bottom-front-left [presses a vertex when the ultracube is not rotating]";
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
