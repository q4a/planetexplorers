using UnityEngine;
using System;

namespace CameraForge
{
	[Menu("Math/Standard/LerpAngle", 10)]
	public class LerpAngle : FunctionNode
	{
		public LerpAngle ()
		{
			A = new Slot ("A");
			B = new Slot ("B");
			T = new Slot ("T");
		}
		
		public override Var Calculate ()
		{
			T.Calculate();

			if (!T.value.isNull)
			{
				float t = T.value.value_f;

				if (t == 0f)
				{
					A.Calculate();
					return A.value;
				}
				else if (t == 1f)
				{
					B.Calculate();
					return B.value;
				}

				A.Calculate();
				B.Calculate();

				Var a = A.value;
				Var b = B.value;
				Var c = Var.Null;
				
				if (!a.isNull && !b.isNull)
				{
					if (a.type == EVarType.Bool && b.type == EVarType.Bool)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					else if (a.type == EVarType.Bool && b.type == EVarType.Int)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					else if (a.type == EVarType.Int && b.type == EVarType.Bool)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					else if (a.type == EVarType.Int && b.type == EVarType.Int)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					else if (a.type == EVarType.Int && b.type == EVarType.Float)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					else if (a.type == EVarType.Float && b.type == EVarType.Int)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					else if (a.type == EVarType.Float && b.type == EVarType.Float)
					{
						c = Mathf.LerpAngle(a.value_f, b.value_f, t);
					}
					
					else if (a.type == EVarType.Vector && b.type == EVarType.Vector)
					{
						Vector4 vec = Vector4.zero;
						vec.x = Mathf.LerpAngle(a.value_v.x, b.value_v.x, t);
						vec.y = Mathf.LerpAngle(a.value_v.y, b.value_v.y, t);
						vec.z = Mathf.LerpAngle(a.value_v.z, b.value_v.z, t);
						vec.w = Mathf.LerpAngle(a.value_v.w, b.value_v.w, t);
						c = vec;
					}
					else if (a.type == EVarType.Quaternion && b.type == EVarType.Quaternion)
					{
						c = Quaternion.Slerp(a.value_q, b.value_q, t);
					}
					else if (a.type == EVarType.Color && b.type == EVarType.Color)
					{
						c = a.value_c * (1f-t) + b.value_c * t;
					}
					else if (a.type == EVarType.String && b.type == EVarType.String)
					{
						c = ((t < 0.5f) ? a.value_str : b.value_str);
					}
				}
				return c;
			}
			return Var.Null;
		}
		
		public override Slot[] slots
		{
			get { return new Slot[3] {A, B, T}; }
		}
		
		public Slot A;
		public Slot B;
		public Slot T;
	}
}
