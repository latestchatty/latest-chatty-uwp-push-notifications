namespace SNPN.Model
{
	public class NewPostEvent
	{
		public string ParentAuthor { get; set; }
		public Post Post { get; set; }
		public int PostId { get; set; }

		public NewPostEvent(int postId, string parentAuthor, Post post)
		{
			Post = post;
			PostId = postId;
			ParentAuthor = parentAuthor;
		}
	}
}