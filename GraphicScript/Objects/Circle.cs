using System.Windows.Shapes;

namespace GraphicScript.Objects
{
    public class Circle : Figure
    {
        public double Radius;

        public Circle()
        {
            Radius = 50;
        }

        protected override Shape CreateShape()
        {
            return new Ellipse();
        }

        protected override void UpdateShape()
        {
            Shape.Width = Shape.Height = Radius;
        }
    }
}