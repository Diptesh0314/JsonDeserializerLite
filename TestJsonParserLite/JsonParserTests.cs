using JsonDeserializerLite;

class Job
{
    public string JobName { get; set; }
}

class Employee
{
    public int EmpId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }

    public Job Jobs { get; set; }

}

class EmployeeWrapper : Employee
{
    public List<Employee> employees { get; set; }
}

public class JsonParserTests
{
    static readonly string[] testJsonData =
        [
            //"{\"data\":{\"firstName\":\"John\", \"lastName\":\"Doe\", \"Roles\": [\"abc\", \"def\"], \"Salary\":[15,-20,56]}}",
            "{\r\n \"employees\": [{\"firstName\":\"John\", \"lastName\":\"Doe\"}]\r\n, \"Jobs\": {\"jobName\": \"Software\" }}",
            "{\"firstName\":\"John\", \"lastName\":\"Doe\", \"Age\":29 }",
            "{\r\n\"employees\":[\r\n  {\"firstName\":\"John\", \"lastName\":\"Doe\"},\r\n  {\"firstName\":\"Anna\", \"lastName\":\"Smith\"},\r\n  {\"firstName\":\"Peter\", \"lastName\":\"Jones\"}\r\n]\r\n}"
        ];

    public static void Main(string[] args)
    {
        JsonParserLite jsonParser = new();
        foreach (string testjson in testJsonData)
        {
            var res = jsonParser.ParseJson<EmployeeWrapper>(testjson);
        }
    }
}