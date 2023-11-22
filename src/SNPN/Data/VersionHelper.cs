namespace SNPN.Data;

public class VersionHelper
{
   public readonly string Version;

   public VersionHelper()
   {
      Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
   }
}