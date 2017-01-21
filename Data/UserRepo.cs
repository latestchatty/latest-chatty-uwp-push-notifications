using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shacknews_Push_Notifications.Data;
using Shacknews_Push_Notifications.Model;

namespace Shacknews_Push_Notifications
{
    public class UserRepo : DBHelper
    {
        public async Task<NotificationUser> FindUser(string userName)
        {
            using (var con = UserRepo.GetConnection())
            {
                return (
                    await con.QueryAsync<NotificationUser>(
                        @"SELECT * FROM User WHERE LOWER(UserName)=@userName",
                        new { userName = userName.ToLower() }
                        )
                    ).FirstOrDefault();
            }
        }
    }
}