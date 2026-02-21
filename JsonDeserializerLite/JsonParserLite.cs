using System.Reflection;
using System.Text;

namespace JsonDeserializerLite
{
    public class JsonParserLite
    {
        private const int MAX_CAP = 1000;

        public T ParseJson<T>(string jsonData) where T : new()
        {
            var stack = new Stack<char>();
            stack.Push('{');
            (Dictionary<string, object> values, int currInd) = HandleCurlyBraces(0, jsonData, stack);

            var emp = DeserializeJsonData(typeof(T), jsonData, values);

            return (T)emp;
        }

        public static object DeserializeJsonData(Type rtnType, string jsonData, Dictionary<string, object> values)
        {
            var res = Activator.CreateInstance(rtnType);
            Type type = res.GetType();

            PropertyInfo[] properties = type.GetProperties();
            foreach (var p in properties)
            {

                if (IsCustomClass(p.PropertyType))
                {
                    if (values.ContainsKey(p.Name.ToLower()))
                    {
                        if (values[p.Name.ToLower()] is Dictionary<string, object> strObj)
                        {
                            object rtnObj = DeserializeJsonData(p.PropertyType, jsonData, strObj);
                            p.SetValue(res, rtnObj);
                            continue;
                        }
                    }
                }

                if (p.PropertyType.IsGenericType)
                {
                    if (values.ContainsKey(p.Name.ToLower()))
                    {
                        var listInstance = Activator.CreateInstance(p.PropertyType);
                        p.SetValue(res, listInstance);
                        Type childType = GetImmediateChildType(p.PropertyType);
                        MethodInfo addMethod = p.PropertyType.GetMethod("Add");
                        object val = values[p.Name.ToLower()];
                        if (val is List<object> listDict)
                        {
                            foreach (var list in listDict)
                            {
                                if (list is Dictionary<string, object> strObj)
                                {
                                    object rtnObj = DeserializeJsonData(childType, jsonData, strObj);
                                    addMethod.Invoke(listInstance, [rtnObj]);
                                }
                            }
                        }
                        p.SetValue(res, listInstance);
                        continue;
                    }
                }

                if (values != null && values.Any(x => x.Key.ToLower() == p.Name.ToLower()))
                {
                    var val = values[p.Name.ToLower()];
                    if (!values[p.Name.ToLower()].GetType().Equals(p.PropertyType.Name) && p.PropertyType.Name.Contains("int32", StringComparison.InvariantCultureIgnoreCase))
                    {
                        val = Convert.ToInt32(val);
                    }

                    p.SetValue(res, val);
                }
            }

            return res;
        }

        static bool IsCustomClass(Type type)
        {
            string nameSpace = type.FullName;
            if (nameSpace.StartsWith("System."))
                return false;

            return type.IsClass;
        }

        static Type GetImmediateChildType(Type objectType)
        {
            Type[] typeArguments = objectType.GetGenericArguments();
            return typeArguments[0];
        }

        static (Dictionary<string, object>, int ind) HandleCurlyBraces(int ind, string jsonData, Stack<char> checkPointChar)
        {
            //List<(string key, object val)> values = new();
            Dictionary<string, object> values = new();
            while (ind < jsonData.Length && checkPointChar.Count > 0)
            {
                (int nextValidCharIndex, char nextValidChar) = GetNextValidCharHelper(ind, jsonData);

                if (Char.IsWhiteSpace(nextValidChar))
                    return (null, -1);

                if (nextValidChar == '}')
                {
                    while (checkPointChar.Count > 0)
                    {
                        var closingCurl = checkPointChar.Pop();
                        if (closingCurl == '{')
                        {
                            ind = nextValidCharIndex;
                            return (values, ind);
                        }
                    }
                }

                if (nextValidChar == ',')
                {
                    ind = nextValidCharIndex;
                    continue;
                }

                if (nextValidChar == '"')
                {
                    (int quoteEndInd, string keyAttribute) = HandleQuotation(nextValidCharIndex, jsonData);
                    keyAttribute = keyAttribute.ToLower();

                    //TODO: Validate the quoteEndInd and keyAttribute
                    (int colonIndex, char colonChar) = GetNextValidCharHelper(quoteEndInd, jsonData);
                    if (colonIndex < 0 && colonChar != ':')
                        throw new InvalidDataException("Invalid data found");

                    //Find next char after ':', it can be '{' , '[', '"'
                    (int valAttrCharInd, char valAttrChar) = GetNextValidCharHelper(colonIndex, jsonData);
                    if (valAttrChar == -1)
                        throw new InvalidDataException("Invalid data found");

                    #region block "

                    if (valAttrChar == '"')
                    {
                        (int quoteInd, string quoteVal) = HandleQuotation(valAttrCharInd, jsonData);
                        AddInDictionary(values, keyAttribute, quoteVal);
                        ind = quoteInd;
                    }

                    #endregion

                    #region block int values
                    if (valAttrChar == '+' || valAttrChar == '-' || Char.IsDigit(valAttrChar))
                    {
                        (int rtnInd, long val) = HandleIntegerValue(valAttrCharInd, jsonData);
                        if (rtnInd == -1)
                            throw new InvalidDataException("Invalid integer value found");

                        AddInDictionary(values, keyAttribute, val);
                        ind = rtnInd;
                    }
                    #endregion

                    #region block {

                    if (valAttrChar == '{')
                    {
                        checkPointChar.Push('{');
                        (Dictionary<string, object> rtnObj, int rtnInd) = HandleCurlyBraces(valAttrCharInd, jsonData, checkPointChar);
                        AddInDictionary(values, keyAttribute, rtnObj);
                        ind = rtnInd;
                    }

                    #endregion

                    #region block [

                    if (valAttrChar == '[')
                    {
                        checkPointChar.Push('[');
                        (List<object> processedSqrBracVals, int processedSqrBracInd) = HandleSqrBrackets(valAttrCharInd, jsonData, checkPointChar);
                        AddInDictionary(values, keyAttribute, processedSqrBracVals);
                        ind = processedSqrBracInd;
                    }

                    #endregion
                }
            }

            return values.Count == 0 ? (null, -1) : (values, ind);
        }

        private static void AddInDictionary(Dictionary<string, object> dict, string key, object val)
        {
            dict[key] = val;
        }

        static (List<object> val, int ind) HandleSqrBrackets(int ind, string jsonData, Stack<char> checkPointChar)
        {
            List<object> values = new List<object>();

            while (ind < jsonData.Length && checkPointChar.Count > 0)
            {
                (int nextValidCharIndex, char nextValidChar) = GetNextValidCharHelper(ind, jsonData);

                if (Char.IsWhiteSpace(nextValidChar))
                    return (null, -1);

                if (nextValidChar == ',')
                {
                    ind = nextValidCharIndex;
                    continue;
                }


                if (nextValidChar == ']')
                {
                    while (checkPointChar.Count > 0)
                    {
                        char squereBrac = checkPointChar.Pop();
                        if (squereBrac == '[')
                        {
                            ind = nextValidCharIndex;
                            return (values, ind);
                        }
                    }
                }


                //handle string values
                if (nextValidChar == '"')
                {
                    (int quoteInd, string quoteVal) = HandleQuotation(nextValidCharIndex, jsonData);
                    //TODO: Validate
                    values.Add(quoteVal);
                    ind = quoteInd;
                }

                //handle int values
                if (nextValidChar == '+' || nextValidChar == '-' || Char.IsDigit(nextValidChar))
                {
                    (int rtnInd, long val) = HandleIntegerValue(nextValidCharIndex, jsonData);
                    if (rtnInd == -1)
                        throw new InvalidDataException("Invalid integer value found");

                    values.Add(val);
                    ind = rtnInd;
                }

                //Handle Arrays
                if (nextValidChar == '{')
                {
                    checkPointChar.Push('{');
                    (Dictionary<string, object> rtnCurlyRes, int curlyInd) = HandleCurlyBraces(nextValidCharIndex, jsonData, checkPointChar);
                    //TODO: Validate
                    values.Add(rtnCurlyRes);
                    ind = curlyInd;
                }
            }

            return values.Count == 0 ? (null, -1) : (values, ind);
        }


        private static (int, char) GetNextValidCharHelper(int ind, string jsonData)
        {
            int nextValidCharIndex = GetNextNonEmptyCharInd(ind, jsonData);
            char nextValidChar = nextValidCharIndex != -1 ? jsonData[nextValidCharIndex] : ' ';
            return (nextValidCharIndex, nextValidChar);
        }

        //Returns the next valid character of the provided index if not found returns -1
        private static int GetNextNonEmptyCharInd(int ind, string data)
        {
            int maxCap = MAX_CAP;
            for (int i = ind + 1; i < data.Length; i++)
            {
                if (maxCap <= 0)
                    return -1;
                if (!Char.IsWhiteSpace(data[i]) && data[i] != '\n' && data[i] != '\r')
                    return i;

                maxCap--;
            }

            return -1;
        }

        private static (int ind, string value) HandleQuotation(int ind, string jsonData)
        {
            int maxCap = MAX_CAP;
            StringBuilder keyOrVal = new StringBuilder();
            int stringEndInd = -1;
            for (int i = ind + 1; i < jsonData.Length; i++)
            {
                if (maxCap <= 0)
                    return (-1, null);

                if (jsonData[i] == '"')
                {
                    return (i, keyOrVal.ToString());
                }
                maxCap--;
                keyOrVal.Append(jsonData[i]);
                stringEndInd = i;
            }

            return (stringEndInd, keyOrVal.ToString());
        }

        private static (int ind, long value) HandleIntegerValue(int ind, string jsonData)
        {
            int maxCap = MAX_CAP;
            StringBuilder val = new StringBuilder();
            HashSet<char> notAllowedCharsInBetween = [',', '{', '}', '[', ']'];
            for (int i = ind; i < jsonData.Length; i++)
            {
                if (maxCap <= 0)
                    return (-1, -1); //Here -1 is treated as invalid, but to validate the return value of this method, validate the index value; first item of the tuple

                if (jsonData[i] == ' ' || jsonData[i] == '\r' || jsonData[i] == '\n')
                    continue;

                if (notAllowedCharsInBetween.Contains(jsonData[i]))
                {
                    ind = i - 1;
                    break;
                }

                if (!Char.IsDigit(jsonData[i]) && jsonData[i] != '+' && jsonData[i] != '-')
                    throw new InvalidDataException("Invalid integer value found");

                val.Append(jsonData[i]);
                maxCap--;
            }

            if (long.TryParse(val.ToString(), out long finalVal))
            {
                return (ind, finalVal);
            }

            return (-1, -1);
        }
    }

}
