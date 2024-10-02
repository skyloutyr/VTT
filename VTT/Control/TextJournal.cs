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
            this.IsPublic = e.GetBool("IsPublic");
            this.IsEditable = e.GetBool("IsEditable");
            this.Title = e.GetString("Title");
            this.Text = e.GetString("Text");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("SelfID", this.SelfID);
            ret.SetGuid("OwnerID", this.OwnerID);
            ret.SetBool("IsPublic", this.IsPublic);
            ret.SetBool("IsEditable", this.IsEditable);
            ret.SetString("Title", this.Title);
            ret.SetString("Text", this.Text);
            return ret;
        }
    }
}
