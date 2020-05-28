using DimensionKing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// The Dimension King
/// Based on the Hyper and Ultracube created by Timwi
/// </summary>
public class DimensionKingModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMRuleSeedable RuleSeedable;
    public KMAudio Audio;
    public Transform DimensionKingObject;
    public Transform BaseVertex;
    public Transform BaseEdge;
    public Transform BaseFace;

    public Material FaceMaterial;

    public Shader DiffuseVertexShader; // KT/Mobile/DiffuseTint
    public Shader TransparentVertexShader; // KT/Transparent/Mobile Diffuse Underlay200

    private static int _moduleIdCounter = 0;
    private int _moduleId;

    private string[] rotations;
    private string schlafli;
    private int vertexCount;

    private int[] calculatedSolveNumbers;

    private Coroutine _rotationCoroutine;
    private bool _transitioning;
    private int solveProgress;
    private List<int> enteredNumbers;


    public static readonly char[] _axesNames = "XYZWVUTSRQPONMLKJIHGFEDCBA".ToCharArray();

    private static readonly string[] possibleShapes = "3 3 3;3 3 4;3 3 5;3 4 3;4 3 3;3 3 3 3;3 3 3 4;4 3 3 3".Split(';');//;5 3 3
    private static readonly string[] possiblePentaShapes = "3 5 5/2;5/2 5 3;5 5/2 5;5 3 5/2;5/2 3 5;5/2 5 5/2;5 5/2 3;3 5/2 5;3 3 5/2;5/2 3 3".Split(';'); // TODO they need testing
    public static readonly string[] inUseShapes = possibleShapes/*.Concat(possiblePentaShapes)*/.ToArray();
    public const int numberOfRotations = 5;
    public static readonly string[] colorNames = "Red;Blue;Yellow;Green;Orange;Cyan;Magenta;Lime;Key;White".Split(';');
    private static readonly Color[] colorValues = "FF0000;0000FF;FFFF00;008000;FF8000;00FFFF;FF00FF;00FF00;000000;FFFFFF".Split(';')
        .Select(x => new Color(Convert.ToByte(x.Substring(0, 2), 16) / 255f, Convert.ToByte(x.Substring(2, 2), 16) / 255f, Convert.ToByte(x.Substring(4, 2), 16) / 255f))
        .ToArray();
    private string[] chosenColors;
    Color originalVertexColor = Color.white;


    internal Color GetColorFromName(string name)
    {
        return colorValues[Array.IndexOf(colorNames, name)];
    }

    private int GetDimensionCount() { return this.geoObject.dimensionCount; }

    private GeoObject geoObject;
    private readonly MonoRandom rand = new MonoRandom();
    private ModuleSolveState moduleState = ModuleSolveState.Rotating;

    private string GetCurrentAxesChars() { return _axesNames.Take(GetDimensionCount()).Join(""); }

    private int GetVertexAndOtherCount(int dimensionCount_n, int faceDimension_m) // m == 0 = vertex // m == 1 = edge // m == 2 = face ...
    {
        if (dimensionCount_n == 0 && faceDimension_m == 0)
            return 1;

        if (dimensionCount_n < faceDimension_m || dimensionCount_n < 0 || faceDimension_m < 0)
            return 0;

        return 2 * GetVertexAndOtherCount(dimensionCount_n - 1, faceDimension_m) + GetVertexAndOtherCount(dimensionCount_n - 1, faceDimension_m - 1);
    }

    [SuppressMessage("Codequality", "IDE0051:Remove unused private members", Justification = "Called by Unity.")]
    void Start()
    {
        this._moduleId = Interlocked.Increment(ref _moduleIdCounter);
        this.originalVertexColor = this.BaseVertex.GetComponent<MeshRenderer>().material.color;

        this.schlafli = inUseShapes.PickRandom();
        //this.schlafli = "4 3 3";

        Log("Picked the following shape: {" + this.schlafli.Replace(' ', ',') + "}");

        SchlafliInterpreter.SchlafliStruct schlafliData = new SchlafliInterpreter.SchlafliStruct();

        try
        {
            schlafliData = SchlafliInterpreter.GetGeometryDataFromSchlafli(this.schlafli.Split(' ')); // TODO allow stuff like "5/2 3 2"
        }
        catch (SchlafliInterpreterException)
        {
            this.Module.HandlePass();
            return;
        }

        this.geoObject = ScriptableObject.CreateInstance<GeoObject>();
        this.geoObject.SetBaseObjects(this.BaseVertex, this.BaseEdge, this.BaseFace);


        float scaleFactor = 2.5f;

        this.geoObject.LoadVerticesEdgesAndFaces(
            schlafliData.VertexLocations.Select(x => x.Select(y => y * scaleFactor).ToArray()).ToArray(), schlafliData.EdgeVertexIndexes, schlafliData.FaceVertexIndexes);


        string[] rotCombinations = GetRotationPermutations(GetDimensionCount());

        this.rotations = Enumerable.Range(0, numberOfRotations).Select(x => rotCombinations[this.rand.Next(rotCombinations.Length)]).ToArray();
        this.vertexCount = schlafliData.VertexLocations.Length;

        this.geoObject.OnVertexClicked += GeoObject_OnVertexClicked;

        Log("Rotations are: " + string.Join(", ", this.rotations));

        this._rotationCoroutine = StartCoroutine(RotateDimKing());
    }

    private void GeoObject_OnVertexClicked(object sender, VertexPressedEventArgs e)
    {
        Log("Clicked vertex " + e.i);

        e.VertexObject.GetKMSelectable().AddInteractionPunch(0.2f);

        //Vertices[v].AddInteractionPunch(.2f);
        //if (_transitioning)
        //    return false;

        if (this.moduleState == ModuleSolveState.Rotating)
        {
            this.moduleState = ModuleSolveState.PreSolving;
            StartCoroutine(RotatingToPreSolve());
            this.enteredNumbers = new List<int>();
        }
        else if (this.moduleState == ModuleSolveState.Solving)
        {
            var c = e.VertexObject.vertexTransform.GetComponent<MeshRenderer>().material.color;
            var pressedColor = colorNames[Array.IndexOf(colorValues, c)];
            Log("Color " + pressedColor + " was pressed.");
            var val = Array.IndexOf(this.chosenColors, pressedColor);
            this.enteredNumbers.Add(val);
            var nums = this.enteredNumbers[0];
            Log("The following number was entered: " + val + ". There are/is " + (this.enteredNumbers[0] - (this.enteredNumbers.Count - 1)) + " number(s) left to be entered.");

            if (this.enteredNumbers.Count == nums + 1) // all numbers have been entered.
            {
                var sum = this.enteredNumbers.Skip(1).Sum();
                if (sum == this.calculatedSolveNumbers[this.solveProgress])
                {
                    Log("Sequence correct.");
                    this.solveProgress++;
                    if (this.solveProgress == this.calculatedSolveNumbers.Length)
                    {
                        this.Module.HandlePass();
                        this.moduleState = ModuleSolveState.Solved;
                    }
                    this.enteredNumbers.Clear();
                }
                else
                {
                    Log("Invalid number entered!");
                    StrikeAndReset();
                }
            }
            else if (this.enteredNumbers.Skip(1).Sum() > this.calculatedSolveNumbers[this.solveProgress])
            {
                Log("The sum of the entered numbers " + this.enteredNumbers[0] + ", [" + this.enteredNumbers.Skip(1).Join(" ") + "] is " +
                    this.enteredNumbers.Skip(1).Sum() + " which is bigger than the correct number " + this.calculatedSolveNumbers[this.solveProgress] + " already, meaning that it cannot be solvedd anymore.\n");
                StrikeAndReset();
            }
        }

        //if (this._rotationCoroutine != null)
        //{
        //    _progress = 0;
        //    StartCoroutine(ColorChange(setVertexColors: true));
        //}
        //else if (v == _correctVertex)
        //{
        //    _progress++;
        //    if (_progress == 4)
        //    {
        //        Debug.LogFormat(@"[The Hypercube #{0}] Module solved.", this._moduleId);
        //        this.Module.HandlePass();
        //        StartCoroutine(ColorChange(keepGrey: true));
        //        this.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, this.transform);
        //    }
        //    else
        //    {
        //        StartCoroutine(ColorChange(setVertexColors: true));
        //    }
        //}
        //else
        //{
        //    Debug.LogFormat(@"[The Hypercube #{0}] Incorrect vertex {1} pressed; resuming rotations.", this._moduleId, StringifyShape(v));
        //    this.Module.HandleStrike();
        //    this._rotationCoroutine = StartCoroutine(RotateHypercube(delay: true));
        //}
    }

    private void StrikeAndReset()
    {
        this.Module.HandleStrike();
        this.solveProgress = 0;
        this._transitioning = false;
        this.enteredNumbers.Clear();
        this.chosenColors = null;
        this.moduleState = ModuleSolveState.Rotating;

        var vo = this.geoObject.GetVertexObjects();
        for (int i = 0; i < vo.Count; i++)
        {
            vo[i].vertexTransform.GetComponent<MeshRenderer>().material.color = this.originalVertexColor;
        }

        StartCoroutine(RotateDimKing());
    }

    private IEnumerator RotatingToPreSolve()
    {
        this._transitioning = true;

        yield return new WaitUntil(() => this._rotationCoroutine == null);

        PlayRandomSound();

        this.chosenColors = colorNames.ToList().Shuffle().Take(this.vertexCount)
            .OrderBy(x => Array.IndexOf(colorNames, x)).ToArray();

        var vo = this.geoObject.GetVertexObjects().ToList().Shuffle().ToArray();

        for (int i = 0; i < vo.Length; i++)
        {
            var c = i < this.chosenColors.Length ? this.chosenColors[i] : this.chosenColors.PickRandom();
            vo[i].vertexTransform.GetComponent<MeshRenderer>().material.color = GetColorFromName(c);
            Log("Assigned color " + c + " with a value of " + GetColorFromName(c).ToString());
        }

        this.calculatedSolveNumbers = GetSolveNumbers();

        Log("The numbers that need to be entered are: " + this.calculatedSolveNumbers.Select(x => SolveNumberToSequence(x).Join("-")).Join(", "));
        Log("The colors that need to be entered are: " + this.calculatedSolveNumbers.Select(x => SolveNumberToSequence(x).Select(y => this.chosenColors[y]).Join("-")).Join(", "));

        this.moduleState = ModuleSolveState.Solving;
    }

    private int[] SolveNumberToSequence(int number)
    {
        if (number == 0)
        {
            return new int[] { 0 };
        }
        else
        {
            int remaining = number;
            var nums = new List<int>();

            while (remaining > 0)
            {
                var n = Math.Min(remaining, this.chosenColors.Length - 1);
                remaining -= n;

                nums.Add(n);
            }

            nums.Insert(0, nums.Count());
            return nums.ToArray();
        }
    }

    public static string[] GetRotationPermutations(int dimCount)
    {
        var relevantAxesNames = _axesNames.Take(dimCount).ToArray();

        return Enumerable.Range(0, relevantAxesNames.Length)
            .SelectMany(i => Enumerable.Range(i + 1, relevantAxesNames.Length - i - 1)
            .Select(j => relevantAxesNames[i].ToString() + relevantAxesNames[j].ToString()))
            .SelectMany(x => new[] { x, x[1].ToString() + x[0].ToString() }).ToArray();
    }

    public static int GetRotationValue(char rotchar1, char rotchar2, int dimensionCount)
    {
        var index1 = Array.IndexOf(_axesNames, rotchar1);
        var index2 = Array.IndexOf(_axesNames, rotchar2);

        return index1 * dimensionCount + index2;
    }

    private void PlayRandomSound()
    {
        this.Audio.PlaySoundAtTransform("Bleep" + Rnd.Range(1, 11), this.transform);
    }

    private IEnumerator RotateDimKing()
    {
        while (!this._transitioning)
        {
            yield return new WaitForSeconds(Rnd.Range(1.75f, 2.25f));

            for (int rot = 0; rot < this.rotations.Length && !this._transitioning; rot++)
            {
                var currRotName = this.rotations[rot];

                var axis1 = GetCurrentAxesChars().IndexOf(currRotName[0]);
                var axis2 = GetCurrentAxesChars().IndexOf(currRotName[1]);
                var duration = 2f;
                var elapsed = 0f;

                float rotationDone = 0f;

                while (elapsed < duration)
                {
                    float currRot = Helpers.GetRotationProgress(elapsed / duration, 3);
                    float delta = Math.Max(0, currRot - rotationDone);
                    rotationDone += delta;

                    this.geoObject.Rotate(axis1, axis2, delta);

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                if (!this._transitioning)
                    yield return new WaitForSeconds(Rnd.Range(.5f, .6f));
            }
            // TODO make it return all vertices to the start. Just linearly :)

            var returnDuration = 2f;
            var returnElapsed = 0f;

            var vertexLocationsAtEnd = this.geoObject.GetVertexLocations;


            while (returnElapsed < returnDuration)
            {
                float currDistance = Helpers.GetRotationProgress(returnElapsed / returnDuration, 3);

                var newPos = Enumerable.Range(0, vertexLocationsAtEnd.Length)
                    .Select(i => this.geoObject.OriginalVertexLocations[i] * currDistance + vertexLocationsAtEnd[i] * (1 - currDistance)).ToArray();

                this.geoObject.SetVertexLocations(newPos);

                yield return null;
                returnElapsed += Time.deltaTime;
            }

            this.geoObject.Reset(); // reset just in case of a floating point error which might cause an angle deviation which may add up over time

            //var colorChange2 = ColorChange(delay: true, skipGrey: true);
            //while (colorChange2.MoveNext())
            //    yield return colorChange2.Current;
        }

        this._transitioning = false;
        this._rotationCoroutine = null;
    }

    private int[] GetSolveNumbers() // gets the numbers that should be entered to solve this module.
    {
        var retval = new List<int>();
        if (this.rotations.Length == 0)
        {
            throw new InvalidOperationException("No rotations defined.");
        }

        foreach (var rot in this.rotations) // calc rot numbers Rn
        {
            retval.Add(GetRotationValue(rot[0], rot[1], GetDimensionCount()));
        }

        foreach (var schlafli in this.schlafli.Split(' ')) // calc schlafli numbers Sn
        {
            if (schlafli.Contains('/'))
            {
                retval.Add(schlafli.Split('/').Sum(x => int.Parse(x)));
            }
            else
            {
                retval.Add(int.Parse(schlafli));
            }
        }

        //retval.Add(this.vertexCount);
        //retval.Add(this.edgeCount);
        //retval.Add(this.faceCount);

        return retval.ToArray();
    }

    private void LogFormat(string text, params string[] args)
    {
        Debug.LogFormat("[DimensionKing #" + this._moduleId + "] " + text, args);
    }

    private void Log(string text)
    {
        Debug.Log("[DimensionKing #" + this._moduleId + "] " + text);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} go [use when ultracube is rotating] | !{0} pong-zig-bottom-front-left [presses a vertex when the ncube is not rotating]";
#pragma warning restore 414

    //IEnumerator ProcessTwitchCommand(string command)
    //{
    //    if (this._rotationCoroutine != null && Regex.IsMatch(command, @"^\s*(go|activate|stop|run|start|on|off)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    //    {
    //        yield return null;
    //        yield return new[] { this.Vertices[0] };
    //        yield break;
    //    }

    //    Match m;
    //    if (this._rotationCoroutine == null && (m = Regex.Match(command, string.Format(@"^\s*((?:{0})(?:[- ,;]*(?:{0}))*)\s*$", _dimensionNames.SelectMany(x => x).Join("|")), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
    //    {
    //        var elements = m.Groups[1].Value.Split(new[] { ' ', ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries);
    //        if (elements.Length != 5)
    //        {
    //            yield return "sendtochaterror Dude, it’s a 5D ultracube, you gotta have 5 dimensions.";
    //            yield break;
    //        }
    //        var dimensions = elements.Select(el => _dimensionNames.ToList().FindIndex(d => d.Any(dn => dn.EqualsIgnoreCase(el)))).ToArray();
    //        var invalid = Enumerable.Range(0, 4).SelectMany(i => Enumerable.Range(i + 1, 4 - i).Where(j => dimensions[i] == dimensions[j]).Select(j => new { i, j })).FirstOrDefault();
    //        if (invalid != null)
    //        {
    //            yield return elements[invalid.i].EqualsIgnoreCase(elements[invalid.j])
    //                ? string.Format("sendtochaterror Dude, you wrote “{0}” twice.", elements[invalid.i], elements[invalid.j])
    //                : string.Format("sendtochaterror Dude, “{0}” and “{1}” doesn’t jive.", elements[invalid.i], elements[invalid.j]);
    //            yield break;
    //        }
    //        var vertexIx = 0;
    //        for (int i = 0; i < 5; i++)
    //            vertexIx |= _dimensionNames[dimensions[i]].ToList().FindIndex(dn => dn.EqualsIgnoreCase(elements[i])) << dimensions[i];
    //        yield return null;
    //        yield return new[] { this.Vertices[vertexIx] };
    //    }
    //}
}
