namespace Model
{
	public class NewPostEvent
	{
		public string ParentAuthor { get; set; }
		public Post Post { get; set; }
		public int PostId { get; set; }

		public NewPostEvent(int postId, string parentAuthor, Post post)
		{
			this.Post = post;
			this.PostId = postId;
			this.ParentAuthor = parentAuthor;
		}
	}
}