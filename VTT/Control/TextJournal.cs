namespace VTT.Control
{
    using System;
    using VTT.Util;

    public class TextJournal : ISerializable
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public Guid SelfID { get; set; }
        public Guid OwnerID { get; set; }
        public bool IsPublic { get; set; }
        public bool IsEditable { get; set; }

        public bool NeedsDeletion { get; set; }
        public bool NeedsSave { get; set; }

        public void Deserialize(DataElement e)
        {
            this.SelfID = e.GetGuid("SelfID");
            this.OwnerID = e.GetGuid("OwnerID");
            this.IsPublic = e.Get<bool>("IsPublic");
            this.IsEditable = e.Get<bool>("IsEditable");
            this.Title = e.Get<string>("Title");
            this.Text = e.Get<string>("Text");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("SelfID", this.SelfID);
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.Set("IsPublic", this.IsPublic);
            ret.Set("IsEditable", this.IsEditable);
            ret.Set("Title", this.Title);
            ret.Set("Text", this.Text);
            return ret;
        }
    }
}
