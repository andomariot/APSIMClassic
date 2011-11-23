using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using CSGeneral;
using System.Collections;



public class LinearInterpolationFunction : Function
{
    [Link]
    Plant Plant = null;

    [Link]
    XYPairs XYPairs = null;

    [Param]
    private string XProperty = "";

    [Output]
    public override double Value
    {
        get
        {
            string PropertyName = XProperty;
            string ArraySpec = StringManip.SplitOffBracketedValue(ref PropertyName, '[', ']');
            double XValue;
            if (ArraySpec == "")
            {
                object v = ExpressionFunction.Evaluate(Plant, XProperty);
                if (v == null)
                    throw new Exception("Cannot find value for "+ Name +" XProperty: " + XProperty);
                XValue = Convert.ToDouble(v);
            }
            else
                XValue = Convert.ToDouble(Plant.GetObject(XProperty));

            return XYPairs.ValueIndexed(XValue);
        }
    }

}

