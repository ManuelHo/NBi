﻿using System.Xml.Serialization;
using NBi.Core.ResultSet;
using System.Collections.Generic;
using System.Linq;

namespace NBi.Xml.Items.ResultSet
{
    public class CellXml : ICell
    {
        [XmlText]
        public string Value { get; set; }

        [XmlAttribute("column-name")]
        public string ColumnName { get; set; }

        public override string ToString()
        {
            return Value;
        }

        [XmlElement("row")]
        public List<RowXml> _rows { get; set; }

        public IList<IRow> Rows
        {
            get { return _rows.Cast<IRow>().ToList(); }
        }

        public CellXml()
        {
            _rows = new List<RowXml>();
        }
    }
}
