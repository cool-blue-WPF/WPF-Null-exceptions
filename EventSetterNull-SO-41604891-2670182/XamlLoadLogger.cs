﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using System.Xaml;
using XamlReader = System.Xaml.XamlReader;


namespace EventSetterNull_SO_41604891_2670182
{
	public class XamlLoadLogger
	{
		private XamlNodeType currentNodeType;
		private int _indent = 0;

		private static readonly Action<XamlLoadLogger> IncreaseIndent =
			(XamlLoadLogger inst) => inst._indent += 1;

		private static readonly Action<XamlLoadLogger> DecreaseIndent =
			(XamlLoadLogger inst) => inst._indent -= 1;

		private readonly Dictionary<string, Func<dynamic, string>> _logTypes = 
			new Dictionary<string, Func<dynamic, string>>
			{
				{ "XamlNodeList.Add", (data) => data.Name ?? data._value},
				{ "null", (x) => null}
			};

		private static readonly int tabStop = 4;
		private static readonly string tab = new string(' ', tabStop);
		private string Padding
		{
			get { return new string(' ', _indent * tabStop); }
		}

		private string Pre(string label)
		{
			return label == "" ? "" : new string(' ', 12) + tab + Padding + label;
		}

		private string LogReader(System.Xaml.XamlReader reader)
		{
			var output = "";
			var info = "";

			Action<XamlLoadLogger> postIndent;
			var preIndent = postIndent = (l) => {};
			var nodeType = reader.NodeType;

			switch (nodeType)
			{
				case XamlNodeType.StartObject:
					info = reader.Type != null ? reader.Type.Name : null;
					postIndent = IncreaseIndent;
					break;
				case XamlNodeType.EndObject:
					info = reader.Type != null ? reader.Type.Name : null;
					preIndent = DecreaseIndent;
					break;
				case XamlNodeType.GetObject:
					//info = reader.SchemaContext?.GetType().ToString();
					postIndent = IncreaseIndent;
					break;
				case XamlNodeType.StartMember:
				case XamlNodeType.EndMember:
					info = reader.Member != null ? reader.Member.Name : null;
					switch (nodeType)
					{
						case XamlNodeType.StartMember:
							postIndent = IncreaseIndent;
							break;
						case XamlNodeType.EndMember:
							preIndent = DecreaseIndent;
							break;
					}
					break;
				case XamlNodeType.Value:
				{
					var value = reader.Value;
					var valueType = value.GetType();
					if (valueType.IsValueType || valueType == typeof(string))
					{
						info = value.ToString();
						break;
					}
					if (reader.Value.GetType() == typeof(System.IO.MemoryStream))
					{
						info =new string( (char[]) new[] {"_binaryReader", "m_charBuffer"}
							.Aggregate((object) reader, getMember));
						break;
					}
					if((info = LogMemberTry(value, 
						   new[] {"_buffer", "_value", "Name", "ResourceKey"})) != "null")
						break;
					info = value.ToString();
					
				}
					break;
				case XamlNodeType.None:
					info = "None";
					break;
				case XamlNodeType.NamespaceDeclaration:
					info = reader.Namespace?.Namespace;
					break;
			}

			preIndent(this);

			output = string.Format("{1,-8}{2,4}{3}{0}{4}{0}{5}", tab, "Line: ",
				((System.Xaml.IXamlLineInfo) reader).LineNumber,
				Padding, nodeType, info);

			postIndent(this);

			return output;
		}

		public string logType(dynamic d, string type)
		{
			if (type == null) return null;
			string res = null;
			switch (type)
			{
				case "XamlNodeList.Add":
					res = d.Name ?? d._value;
					break;
			}
			return res;
		}

		public string Log(string caller, object arg, string type = null)
		{
			if (arg == null) return caller + ": Log Error: nul arg";
			if (arg is XamlReader) return LogReader((XamlReader) arg);
			if (arg is string) return Pre(caller + " ") + (string)(arg);
			return Pre(caller + " ") + (logType(arg, type) ?? "null");
			return Pre(caller + " ") +( _logTypes[type?? "null"](arg) ?? "null");
		}

		public string LogMember(object host, object member, string label = "")
		{
			if (member is string) return Pre(label) + getMember(host, (string) member);

			var members = member as string[];
			if (members == null)
				return Pre(label) + "Argument Exception: member must be string or string[]";
			return Pre(label) + members.Aggregate(host, getMember);
		}

		public string LogMemberTry(object host, string[] options, string label = "")
		{
			var result = "";
			var i = 0;

			if (host == null || host.GetType().IsValueType || host is string)
				return Pre(label) + (host?.ToString() ?? "null");

			foreach (var option in options) // return the first viable option
			{
				var _value = getValue2(host, option);
				if (_value == null) continue;
				if ((result = _value.ToString() ?? "null") != "null")
					break;
			}
			return Pre(label) + result;
		}

		private object getMember(object host, string member)
		{
			if(host == null) return "null";
			return getValue2(host, member) ?? host.ToString();
		}

		private object getValue(object host, string member)
		{
			object retVal = null;
			const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var hostType = host.GetType();
			var members = hostType.GetMember(member, bindingFlags);

			while (members.Length == 0 && hostType != typeof(object))
			{
				hostType = hostType.BaseType ?? typeof(object);
				members = hostType.GetMember(member, bindingFlags);
			}

			if (members.Length == 0)
				throw new ValueUnavailableException(string.Format("{0}: Member not found",
					host));

			members = members.Where(
				m =>
					(m.MemberType == MemberTypes.Field)
					|| (m.MemberType == MemberTypes.Property)
			).ToArray();


			if (members.Length == 0) throw new ValueUnavailableException();

			switch (members[0].MemberType) // first field or property
			{
				case MemberTypes.Property:
					retVal = hostType.GetProperty(member, bindingFlags)
						.GetValue(host);
					break;
				case MemberTypes.Field:
					var memberInfo = hostType.GetField(member, bindingFlags);
					if (memberInfo != null)
						retVal = memberInfo.GetValue(host);
					break;
			}
			return retVal;
		}

		private object getValue2 (object host, string member)
		{
			object retVal = null;
			const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var hostType = host.GetType();
			var members = hostType.GetMember(member, bindingFlags);

			while (members.Length == 0 && hostType != typeof(object))
			{
				hostType = hostType.BaseType ?? typeof(object);
				members = hostType.GetMember(member, bindingFlags);
			}

			if (members.Length == 0) return null;

			members = members.Where(
				m =>
					(m.MemberType == MemberTypes.Field)
					|| (m.MemberType == MemberTypes.Property)
			).ToArray();


			if (members.Length == 0) return null;

			switch (members[0].MemberType) // first field or property
			{
				case MemberTypes.Property:
					retVal = hostType.GetProperty(member, bindingFlags)
						.GetValue(host);
					break;
				case MemberTypes.Field:
					var memberInfo = hostType.GetField(member, bindingFlags);
					if (memberInfo != null)
						retVal = memberInfo.GetValue(host);
					break;
			}
			return retVal;
		}

		public string LogMemberList<T>(List<T> host, string label = "", int indx = -1)
		{
			var pre = new string(' ', 12) + Padding + label;
			pre += string.Format("{0,-6}",indx > -1 ? "[" + indx + "]" : "");
			try
			{
				return pre + host[indx];
			}
			catch (Exception e)
			{
				if (host == null)
				{
					return pre + "null";
				}
				return pre + host;
			}
		}

		public string LogObjectsAsString(object[] o)
		{
			return (string) o.Aggregate((agg, n) => agg + ";" + n);
		}
	}
}