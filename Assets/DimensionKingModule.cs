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
public class DimensionKingModule : MonoBehaviour {
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMRuleSeedable RuleSeedable;
    public KMAudio Audio;
    public Transform RotaterObject;
    public Transform DimensionKingObject;
    public Transform BaseVertex;
    public Transform BaseEdge;
    public Transform BaseFace;

    public Material FaceMaterial;

    public Shader DiffuseVertexShader; // KT/Mobile/DiffuseTint
    public Shader TransparentVertexShader; // KT/Transparent/Mobile Diffuse Underlay200

    private static int _moduleIdCounter = 0;
    private int _moduleId;
    private int randRot;
    private string[] rotations;
    private string schlafli;
    private int vertexCount;

    private int[] calculatedSolveNumbers;

    private Coroutine _rotationCoroutine;
    private bool _transitioning;
    private bool _solveExecuted;
    private int solveProgress;
    private List<int> enteredNumbers;


    public static readonly char[] _axesNames = "XYZWVUTSRQPONMLKJIHGFEDCBA".ToCharArray();

    private const int baseScore = 28;

    private static readonly Dictionary<string, int> possibleShapes = //"3 3 3;3 3 4;3 3 5;3 4 3;4 3 3;3 3 3 3;3 3 3 4;4 3 3 3".Split(';');//;5 3 3
        new Dictionary<string, int>()
        {
            { "3 3 3", 40},
            { "3 3 4", 36},
            { "3 3 5", 30},
            { "3 4 3", 32},
            { "4 3 3", 28},
            { "3 3 3 3", 45},
            { "3 3 3 4", 42},
            { "4 3 3 3", 35},
            //{ "5 3 3", 10},
        };

    [SuppressMessage("Codequality", "IDE0052", Justification = "Used in the future.")]
    private static readonly string[] possiblePentaShapes = "3 5 5/2;5/2 5 3;5 5/2 5;5 3 5/2;5/2 3 5;5/2 5 5/2;5 5/2 3;3 5/2 5;3 3 5/2;5/2 3 3".Split(';'); // TODO they need testing
    public static readonly Dictionary<string, int> inUseShapes = possibleShapes/*.Concat(possiblePentaShapes)*/;
    public const int numberOfRotations = 5;
    public static readonly string[] colorNames = "Red;Blue;Yellow;Green;Orange;Cyan;Magenta;Lime;Key;White".Split(';');
    private static readonly Color[] colorValues = "FF0000;0000FF;FFFF00;008000;FF8000;00FFFF;FF00FF;00FF00;000000;FFFFFF".Split(';')
        .Select(x => new Color(Convert.ToByte(x.Substring(0, 2), 16) / 255f, Convert.ToByte(x.Substring(2, 2), 16) / 255f, Convert.ToByte(x.Substring(4, 2), 16) / 255f))
        .ToArray();
    private string[] chosenColors;
    private Color originalVertexColor = Color.white;


    internal Color GetColorFromName(string name) {
        return colorValues[Array.IndexOf(colorNames, name)];
    }

    private int GetDimensionCount() { return geoObject.dimensionCount; }

    private int? GetModuleScore() {
        if (inUseShapes.ContainsKey(schlafli)) {
            return inUseShapes[schlafli] - baseScore;
        } else {
            Log("no score found for '" + schlafli + "'? Autosolving module...");
            Module.HandlePass();
            return null;
        }
    }

    private GeoObject geoObject;
    private static readonly MonoRandom rand = new MonoRandom();
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
    private void Start() {
        _moduleId = Interlocked.Increment(ref _moduleIdCounter);
        var nonZeroModuleRotationAngleProbability = 0.25f;
        randRot = Rnd.Range(0f, 1f) < nonZeroModuleRotationAngleProbability ? Rnd.Range(1, 4) * 90 : 0; // either 0, 90, 180, 270 degrees
        Log("Module rotation angle is " + randRot + (randRot == 0 ? "." : ". Oh no :("));

        originalVertexColor = BaseVertex.GetComponent<MeshRenderer>().material.color;

        schlafli = inUseShapes.Keys.PickRandom();
        //this.schlafli = "4 3 3";

        Log("Picked the following shape: {" + schlafli.Replace(' ', ',') + "}");

        SchlafliInterpreter.SchlafliStruct schlafliData = new SchlafliInterpreter.SchlafliStruct();

        try {
            schlafliData = SchlafliInterpreter.GetGeometryDataFromSchlafli(schlafli.Split(' '));
        } catch (SchlafliInterpreterException) {
            Module.HandlePass();
            return;
        }

        geoObject = ScriptableObject.CreateInstance<GeoObject>();
        geoObject.SetBaseObjects(BaseVertex, BaseEdge, BaseFace);


        float scaleFactor = 2.5f;

        geoObject.LoadVerticesEdgesAndFaces(
            schlafliData.VertexLocations.Select(x => x.Select(y => y * scaleFactor).ToArray()).ToArray(), schlafliData.EdgeVertexIndexes, schlafliData.FaceVertexIndexes);


        string[] rotCombinations = GetRotationPermutations(GetDimensionCount());

        rotations = Enumerable.Range(0, numberOfRotations).Select(x => rotCombinations[rand.Next(rotCombinations.Length)]).ToArray();
        vertexCount = schlafliData.VertexLocations.Length;

        geoObject.OnVertexClicked += (sender, e) => OnDkVertexClicked(sender, e);

        Log("Rotations are: " + string.Join(", ", rotations));

        _rotationCoroutine = StartCoroutine(RotateDimKing());
    }

    [SuppressMessage("Codequality", "IDE0051:Remove unused private members", Justification = "Called by Unity.")]
    private void Update() {
        if (randRot != 0) {
            RotaterObject.localRotation = Quaternion.Euler(RotaterObject.localRotation.eulerAngles.x, randRot, RotaterObject.localRotation.eulerAngles.z);
            randRot = 0;
        }
    }

    private VertexClickResult OnDkVertexClicked(object sender, VertexPressedEventArgs e) {
        if (moduleState == ModuleSolveState.Solved)
            return VertexClickResult.None;

        Log("Clicked vertex " + e.i);

        Audio.PlaySoundAtTransform("Bleep" + new[] { 3, 4, 6 }.PickRandom(), transform);
        e.VertexObject.GetKMSelectable().AddInteractionPunch(0.2f);

        //Vertices[v].AddInteractionPunch(.2f);
        //if (_transitioning)
        //    return false;


        if (moduleState == ModuleSolveState.Rotating) {
            moduleState = ModuleSolveState.PreSolving;
            StartCoroutine(RotatingToPreSolve());
            enteredNumbers = new List<int>();
            return VertexClickResult.None;
        } else if (moduleState == ModuleSolveState.Solving) {
            var c = e.VertexObject.vertexTransform.GetComponent<MeshRenderer>().material.color;
            var pressedColor = colorNames[Array.IndexOf(colorValues, c)];
            Log("Color " + pressedColor + " was pressed.");
            var val = Array.IndexOf(chosenColors, pressedColor);
            enteredNumbers.Add(val);
            var nums = enteredNumbers[0];
            var currentNumberSum = enteredNumbers.Skip(1).Sum();

            Log("The following number was entered: " + val + ". There are/is " + (enteredNumbers[0] - (enteredNumbers.Count - 1)) + " number(s) left to be entered.");
            Log(currentNumberSum + (enteredNumbers[0] - (enteredNumbers.Count - 1)) * (chosenColors.Length - 1) + " >= " + calculatedSolveNumbers[solveProgress]);

            if (enteredNumbers.Count == nums + 1) // all numbers have been entered.
            {
                if (currentNumberSum == calculatedSolveNumbers[solveProgress]) {
                    Log("Sequence correct.");
                    solveProgress++;
                    if (solveProgress == calculatedSolveNumbers.Length) {
                        moduleState = ModuleSolveState.Solved;
                        StartCoroutine(SolvedAnimation());
                    }
                    enteredNumbers.Clear();
                    return VertexClickResult.WillSolve;
                } else {
                    Log("Invalid number entered!");
                    StrikeAndReset();
                    return VertexClickResult.WillStrike;
                }
            } else if (currentNumberSum > calculatedSolveNumbers[solveProgress]) {
                Log("The sum of the entered numbers " + enteredNumbers[0] + ", [" + enteredNumbers.Skip(1).Join(" ") + "] is " +
                    currentNumberSum + " which is bigger than the correct number " + calculatedSolveNumbers[solveProgress] + " already, meaning that it cannot be solved anymore.\n");
                StrikeAndReset();
                return VertexClickResult.WillStrike;
            } else if (currentNumberSum // if currently entered number sum + (numbers left to enter)*maxNumberValue, so the currently maximum enterable number ...
                  + (enteredNumbers[0] - (enteredNumbers.Count - 1)) * (chosenColors.Length - 1)
                  < calculatedSolveNumbers[solveProgress]) // ... is less than the required, then strike
              {
                Log("The sum of the entered numbers " + enteredNumbers[0] + ", [" + enteredNumbers.Skip(1).Join(" ") + "] is " + currentNumberSum + ". " +
                    "If you add " + ((enteredNumbers[0] - (enteredNumbers.Count - 1)) * (chosenColors.Length - 1)) + ", which is what you could enter at most, " +
                    "then the resulting value is smaller than " + calculatedSolveNumbers[solveProgress] + ", meaning that it cannot be solved anymore.\n");
                StrikeAndReset();
                return VertexClickResult.WillStrike;
            }
        }
        return VertexClickResult.None;
    }

    private void StrikeAndReset() {
        Module.HandleStrike();
        solveProgress = 0;
        _transitioning = false;
        enteredNumbers.Clear();
        chosenColors = null;
        moduleState = ModuleSolveState.Rotating;

        var vo = geoObject.GetVertexObjects();
        for (int i = 0; i < vo.Count; i++) {
            vo[i].vertexTransform.GetComponent<MeshRenderer>().material.color = originalVertexColor;
        }

        _rotationCoroutine = StartCoroutine(RotateDimKing());
    }

    private IEnumerator RotatingToPreSolve() {
        _transitioning = true;

        yield return new WaitUntil(() => _rotationCoroutine == null);

        PlayRandomSound();

        chosenColors = colorNames.ToList().Shuffle().Take(vertexCount)
            .OrderBy(x => Array.IndexOf(colorNames, x)).ToArray();

        var verts = geoObject.GetVertexObjects().ToList();
        var centerLoc = verts.Select(x => x.ProjectTo3D()).Aggregate((a, b) => a + b) / verts.Count;
        var vo = verts.OrderByDescending(x => Vector3.Distance(x.ProjectTo3D(), centerLoc)).ToArray().Shuffle();

        for (int i = 0; i < vo.Length; i++) {
            var c = i < chosenColors.Length ? chosenColors[i] : chosenColors.PickRandom();
            vo[i].vertexTransform.GetComponent<MeshRenderer>().material.color = GetColorFromName(c);
            //Log("Assigned color " + c + " with a value of " + GetColorFromName(c).ToString());
        }

        calculatedSolveNumbers = GetSolveNumbers();

        Log("The numbers that need to be entered are: " + calculatedSolveNumbers.Select(x => SolveNumberToSequence(x).Join("-")).Join(", "));
        Log("The colors that need to be entered are: " + calculatedSolveNumbers.Select(x => SolveNumberToSequence(x).Select(y => chosenColors[y]).Join("-")).Join(", "));

        moduleState = ModuleSolveState.Solving;
    }

    private int[] SolveNumberToSequence(int number) {
        if (number == 0) {
            return new int[] { 0 };
        } else {
            int remaining = number;
            var nums = new List<int>();

            while (remaining > 0) {
                var n = Math.Min(remaining, chosenColors.Length - 1);
                remaining -= n;

                nums.Add(n);
            }

            nums.Insert(0, nums.Count());
            return nums.ToArray();
        }
    }

    public static string[] GetRotationPermutations(int dimCount) {
        var relevantAxesNames = _axesNames.Take(dimCount).ToArray();

        return Enumerable.Range(0, relevantAxesNames.Length)
            .SelectMany(i => Enumerable.Range(i + 1, relevantAxesNames.Length - i - 1)
            .Select(j => relevantAxesNames[i].ToString() + relevantAxesNames[j].ToString()))
            .SelectMany(x => new[] { x, x[1].ToString() + x[0].ToString() }).ToArray();
    }

    public static int GetRotationValue(char rotchar1, char rotchar2, int dimensionCount) {
        var index1 = Array.IndexOf(_axesNames, rotchar1);
        var index2 = Array.IndexOf(_axesNames, rotchar2);

        return index1 * dimensionCount + index2;
    }

    private void PlayRandomSound() {
        Audio.PlaySoundAtTransform("Bleep" + Rnd.Range(1, 11), transform);
    }

    private IEnumerator RotateDimKing() {
        while (!_transitioning) {
            yield return new WaitForSeconds(Rnd.Range(1.75f, 2.25f));

            for (int rot = 0; rot < rotations.Length && !_transitioning; rot++) {
                var currRotName = rotations[rot];

                var axis1 = GetCurrentAxesChars().IndexOf(currRotName[0]);
                var axis2 = GetCurrentAxesChars().IndexOf(currRotName[1]);
                var duration = 2f;
                var elapsed = 0f;

                float rotationDone = 0f;

                while (elapsed < duration) {
                    float currRot = Helpers.GetRotationProgress(elapsed / duration, 3);
                    float delta = Math.Max(0, currRot - rotationDone);
                    rotationDone += delta;

                    geoObject.Rotate(axis1, axis2, delta);

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                if (!_transitioning)
                    yield return new WaitForSeconds(Rnd.Range(.5f, .6f));
            }

            var returnDuration = 2f;
            var returnElapsed = 0f;

            var vertexLocationsAtEnd = geoObject.GetVertexLocations;


            while (returnElapsed < returnDuration) {
                float currDistance = Helpers.GetRotationProgress(returnElapsed / returnDuration, 3);

                var newPos = Enumerable.Range(0, vertexLocationsAtEnd.Length)
                    .Select(i => geoObject.OriginalVertexLocations[i] * currDistance + vertexLocationsAtEnd[i] * (1 - currDistance)).ToArray();

                geoObject.SetVertexLocations(newPos);

                yield return null;
                returnElapsed += Time.deltaTime;
            }

            geoObject.Reset(); // reset just in case of a floating point error which might cause an angle deviation which may add up over time

            //var colorChange2 = ColorChange(delay: true, skipGrey: true);
            //while (colorChange2.MoveNext())
            //    yield return colorChange2.Current;
        }

        _transitioning = false;
        _rotationCoroutine = null;
    }

    private IEnumerator SolvedAnimation() {
        float checkmarkWidth = 0.4f;


        //var checkmarkShapeList = new List<float[]> {
        //    new[] { 0f, 0, 0.2f }, new[] { 0.6f, 0, 0f }, new[] { 0.6f, 0, 2f },
        //    new[] { 1f, 0, 2f }, new[] { 0.7f+0.58f/5+0.3f, 0, /*-1.2f-2.8f/5*/ -0.4f }, new[]{ 0f, 0, -0.2f }
        //};
        var checkmarkShapeList = new List<float[]> {
            //     RIGHT, 0, UP
            new[] { -0.7f, 0, 0.85f }, new[] { 0f, 0, 0.35f }, new[] { 0, 0, 2f },
            new[] { checkmarkWidth, 0, 2f }, new[] { checkmarkWidth, 0,-checkmarkWidth}, new[]{ -0.7f, 0, 0.85f- checkmarkWidth }
        };
        var edgeIndices = Enumerable.Range(0, checkmarkShapeList.Count - 1).Select(x => new[] { x, x + 1 }).ToList();
        edgeIndices.Add(new[] { 0, checkmarkShapeList.Count - 1 });

        var itemsToClone = checkmarkShapeList.Count;
        for (int i = 0; i < itemsToClone; i++) {
            var f = checkmarkShapeList[i].ToArray();
            f[1] = 1;
            checkmarkShapeList.Add(f);

            if (i > 0) {
                edgeIndices.Add(new[] { itemsToClone + i - 1, itemsToClone + i });
            }
            edgeIndices.Add(new[] { i, itemsToClone + i }); // vertical connections
        }
        edgeIndices.Add(new[] { itemsToClone, itemsToClone + itemsToClone - 1 });




        var checkmarkShape = checkmarkShapeList.Select(x => x.Concat(new float[] { 0, 0 }).ToArray()).ToArray();


        yield return geoObject.PhaseToNewObjectAndSetMaterialColor(checkmarkShape, edgeIndices.ToArray(), new int[][] { }, Color.green);
        geoObject.OriginalVertexLocations = geoObject.GetVertexLocations;

        Module.HandlePass();
        _solveExecuted = true;

        while (Bomb.IsBombPresent()) // TODO check if this properly ends the subroutine when the bomb is solved / exploded.
        {
            yield return new WaitForSeconds(Rnd.Range(0.75f, 1f));

            var cmrots = GetRotationPermutations(5).Shuffle().Take(5).ToArray();
            var cmrotsMult = Enumerable.Range(0, cmrots.Length).Select(x => Rnd.Range(0.5f, 3f)).ToArray();


            for (int rot = 0; rot < cmrots.Length && !_transitioning; rot++) {
                var currRotName = cmrots[rot];
                var axis1 = GetCurrentAxesChars().IndexOf(currRotName[0]);
                var axis2 = GetCurrentAxesChars().IndexOf(currRotName[1]);
                var duration = 2f * cmrotsMult[rot];
                var elapsed = 0f;

                float rotationDone = 0f;

                while (elapsed < duration) {
                    float currRot = Helpers.GetRotationProgress(elapsed / duration, 3) * 1;
                    float delta = Math.Max(0, currRot - rotationDone);
                    rotationDone += delta;

                    geoObject.Rotate(axis1, axis2, delta);

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                if (!_transitioning)
                    yield return new WaitForSeconds(Rnd.Range(.2f, .3f));
            }

            var returnDuration = 2f;
            var returnElapsed = 0f;

            var vertexLocationsAtEnd = geoObject.GetVertexLocations;


            while (returnElapsed < returnDuration) {
                float currDistance = Helpers.GetRotationProgress(returnElapsed / returnDuration, 3);

                var newPos = Enumerable.Range(0, vertexLocationsAtEnd.Length)
                    .Select(i => geoObject.OriginalVertexLocations[i] * currDistance + vertexLocationsAtEnd[i] * (1 - currDistance)).ToArray();

                geoObject.SetVertexLocations(newPos);

                yield return null;
                returnElapsed += Time.deltaTime;
            }

            geoObject.Reset(); // reset just in case of a floating point error which might cause an angle deviation which may add up over time
        }
    }

    private int[] GetSolveNumbers() // gets the numbers that should be entered to solve this module.
    {
        var retval = new List<int>();
        if (rotations.Length == 0) {
            throw new InvalidOperationException("No rotations defined.");
        }

        foreach (var rot in rotations) // calc rot numbers Rn
        {
            retval.Add(GetRotationValue(rot[0], rot[1], GetDimensionCount()));
        }

        foreach (var schlafli in schlafli.Split(' ').Take(2)) // calc schlafli numbers Sn
        {
            if (schlafli.Contains('/')) {
                retval.Add(schlafli.Split('/').Sum(x => int.Parse(x)));
            } else {
                retval.Add(int.Parse(schlafli));
            }
        }

        //retval.Add(this.vertexCount);
        //retval.Add(this.edgeCount);
        //retval.Add(this.faceCount);

        return retval.ToArray();
    }

    private void Log(string text) {
        Debug.Log("[Dimension King #" + _moduleId + "] " + text);
    }

#pragma warning disable 414

    [SuppressMessage("Codequality", "IDE0051:Remove unused private members", Justification = "Used by Twitchplays.")]
    private readonly string TwitchHelpMessage = @"!{0} go [use to begin entering the solution] | !{0} press color1 color2 color3 [clicks vertices with those colors, in that order, accepts any args, also allows only entering the first letter of the color]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string commandText) {
        var splitted = commandText.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var cmd = splitted[0];
        var args = splitted.Skip(1).ToArray();


        if (cmd == "go") // if its the command to stop rotations
        {
            if (_rotationCoroutine != null) // and rotating
            {
                yield return null;
                yield return new[] { BaseVertex.GetComponent<KMSelectable>() };

                var score = GetModuleScore();
                if (score.HasValue && score.Value != 0) // set dynamic tp scoring, if a score is returned
                    yield return "awardpointsonsolve " + GetModuleScore().Value.ToString();

                yield break;
            }
        } else if (cmd == "press") {
            if (_rotationCoroutine == null) // and not rotating
            {
                var parsedColors = new List<string>(); // stores the first letter of every color to click

                for (int i = 0; i < args.Length; i++) {
                    var colorArg = args[i];
                    string color = colorNames.FirstOrDefault(x => (colorArg.Length == 1 ? x.Substring(0, 1) : x).Equals(colorArg, StringComparison.InvariantCultureIgnoreCase));

                    if (color == null) {
                        yield return "sendtochaterror The color '" + colorArg + "' was not found!";
                        yield break;
                    }

                    if (chosenColors.Contains(color)) {
                        parsedColors.Add(color);
                    } else {
                        yield return "sendtochaterror The chosen color '" + colorArg + "' is not present!";
                        yield break;
                    }
                }

                if (!parsedColors.Any()) {
                    yield return "sendtochaterror You need to enter one or more colors after the 'press' command!";
                    yield break;
                }

                var vertexCopy = geoObject.GetVertexObjects();
                var KmSelByChar = chosenColors.ToDictionary(x =>
                    x.ToLowerInvariant()[0],
                    x => vertexCopy.First(y => y.GetTransform().GetComponent<MeshRenderer>().material.color == GetColorFromName(x))
                );

                var verticesToClick = parsedColors.Select(x => KmSelByChar[x.ToLowerInvariant()[0]]).ToArray();

                yield return null;
                yield return verticesToClick;

                foreach (var toClick in verticesToClick) {
                    var idx = vertexCopy.IndexOf(toClick);
                    if (idx <= -1)
                        throw new Exception("somehow idx wasnt found???");

                    var clickResult = OnDkVertexClicked(geoObject, new VertexPressedEventArgs(toClick, idx));
                    switch (clickResult) {
                        case VertexClickResult.None:
                            break;
                        case VertexClickResult.WillStrike:
                            Log("Awarding TP strike");
                            yield return "strike";
                            break;
                        case VertexClickResult.WillSolve:
                            Log("Awarding TP solve");
                            yield return "solve";
                            break;
                    }
                }
            }
        }
    }

    private IEnumerator InteractWithKmSelectables(IEnumerator enumerator) {
        while (enumerator.MoveNext()) {
            var res = enumerator.Current;
            if (res is KMSelectable) {
                ((KMSelectable)res).OnInteract();
                yield return null;
            } else if (res is IEnumerable<KMSelectable>) {
                var collection = (IEnumerable<KMSelectable>)res;
                foreach (var item in collection) {
                    item.OnInteract();
                    yield return null;
                }
            }
        }
    }

    private IEnumerator ProperTPCommand(string command) {
        yield return InteractWithKmSelectables(ProcessTwitchCommand(command));
    }

    [SuppressMessage("Codequality", "IDE0051:Remove unused private members", Justification = "Used by Twitchplays.")]
    private IEnumerator TwitchHandleForcedSolve() {
        if (_rotationCoroutine != null) {
            yield return ProperTPCommand("go");
            int failsafeCounter = 15;

            while (_rotationCoroutine != null && (failsafeCounter-- > 0)) {
                yield return new WaitForSeconds(0.5f);
            }

            if (failsafeCounter <= 0) {
                Log("Warning: failsafe counter is " + failsafeCounter);
            }
        }

        for (int i = solveProgress; i < calculatedSolveNumbers.Length; i++) {
            var currSn = calculatedSolveNumbers[i];

            if (!enteredNumbers.Any()) {
                yield return new WaitForSeconds(0.25f);
                yield return ProperTPCommand("press " + chosenColors[(int)Math.Ceiling(currSn / (chosenColors.Length - 1f))]);
            }

            var colorsToPress = new List<string>();

            int sum = enteredNumbers.Skip(1).Sum();

            for (int j = enteredNumbers.Count - 1; j < enteredNumbers[0]; j++) {
                var currValue = Math.Min(chosenColors.Length - 1, currSn - sum);

                colorsToPress.Add(chosenColors[currValue].Substring(0, 1));

                sum += currValue;
            }

            yield return null;
            yield return ProperTPCommand("press " + colorsToPress.Join(" "));
        }

        while (!_solveExecuted) yield return true;
    }
}
