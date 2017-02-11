using System;

namespace SNPN.Common
{
	[Flags]
	public enum ResponseResult
	{
		Success,
		FailDoNotTryAgain,
		FailTryAgain,
		RemoveUser,
		InvalidateToken
	}
}