using AillieoUtils.UnityEmbeddedServers;

namespace Sample
{
    [Controller("sample")]
    public partial class SampleController
    {
        [HttpMethod("Get")]
        public string Index()
        {
            return
@"<!DOCTYPE html>
<html>
<body>

<form action='/sample/add'>
  <label for='num1'>First number: </label>
  <input type='text' id='num1' name='num1'><br><br>
  <label for='num2'>Second number: </label>
  <input type='text' id='num2' name='num2'><br><br>
  <input type='submit' value='Submit'>
</form>

</body>
</html>";
        }

        [HttpMethod("Get")]
        public string Add(string num1, string num2)
        {
            return $"{num1}+{num2}={int.Parse(num1) + int.Parse(num2)}";
        }
    }
}
