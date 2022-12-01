using System;

namespace FeatureLoom.Extensions
{
    public static class TupleExtensions
    {
        public static bool TryOut<P2>(this ValueTuple<bool, P2> tuple, out P2 p2)
        {
            bool p1;
            (p1, p2) = tuple;
            return p1;
        }

        public static bool TryOut<P2, P3>(this ValueTuple<bool, P2, P3> tuple, out P2 p2, out P3 p3)
        {
            bool p1;
            (p1, p2, p3) = tuple;
            return p1;
        }

        public static bool TryOut<P2, P3, P4>(this ValueTuple<bool, P2, P3, P4> tuple, out P2 p2, out P3 p3, out P4 p4)
        {
            bool p1;
            (p1, p2, p3, p4) = tuple;
            return p1;
        }

        public static bool TryOut<P2, P3, P4, P5>(this ValueTuple<bool, P2, P3, P4, P5> tuple, out P2 p2, out P3 p3, out P4 p4, out P5 p5)
        {
            bool p1;
            (p1, p2, p3, p4, p5) = tuple;
            return p1;
        }

        public static P1 ReturnOut<P1,P2>(this ValueTuple<P1, P2> tuple, out P2 p2)
        {
            P1 p1;
            (p1, p2) = tuple;
            return p1;
        }

        public static P1 ReturnOut<P1, P2, P3>(this ValueTuple<P1, P2, P3> tuple, out P2 p2, out P3 p3)
        {
            P1 p1;
            (p1, p2, p3) = tuple;
            return p1;
        }

        public static P1 ReturnOut<P1, P2, P3, P4>(this ValueTuple<P1, P2, P3, P4> tuple, out P2 p2, out P3 p3, out P4 p4)
        {
            P1 p1;
            (p1, p2, p3, p4) = tuple;
            return p1;
        }

        public static P1 ReturnOut<P1, P2, P3, P4, P5>(this ValueTuple<P1, P2, P3, P4, P5> tuple, out P2 p2, out P3 p3, out P4 p4, out P5 p5)
        {
            P1 p1;
            (p1, p2, p3, p4, p5) = tuple;
            return p1;
        }

        public static void AllOut<P1, P2>(this ValueTuple<P1, P2> tuple, out P1 p1, out P2 p2)
        {
            (p1, p2) = tuple;
        }

        public static void AllOut<P1, P2, P3>(this ValueTuple<P1, P2, P3> tuple, out P1 p1, out P2 p2, out P3 p3)
        {
            (p1, p2, p3) = tuple;
        }

        public static void AllOut<P1, P2, P3, P4>(this ValueTuple<P1, P2, P3, P4> tuple, out P1 p1, out P2 p2, out P3 p3, out P4 p4)
        {
            (p1, p2, p3, p4) = tuple;
        }

        public static void AllOut<P1, P2, P3, P4, P5>(this ValueTuple<P1, P2, P3, P4, P5> tuple, out P1 p1, out P2 p2, out P3 p3, out P4 p4, out P5 p5)
        {
            (p1, p2, p3, p4, p5) = tuple;
        }

        public static bool TryOut<P2>(this Tuple<bool, P2> tuple, out P2 p2)
        {
            bool p1;
            (p1, p2) = tuple;
            return p1;
        }

        public static bool TryOut<P2, P3>(this Tuple<bool, P2, P3> tuple, out P2 p2, out P3 p3)
        {
            bool p1;
            (p1, p2, p3) = tuple;
            return p1;
        }

        public static bool TryOut<P2, P3, P4>(this Tuple<bool, P2, P3, P4> tuple, out P2 p2, out P3 p3, out P4 p4)
        {
            bool p1;
            (p1, p2, p3, p4) = tuple;
            return p1;
        }

        public static bool TryOut<P2, P3, P4, P5>(this Tuple<bool, P2, P3, P4, P5> tuple, out P2 p2, out P3 p3, out P4 p4, out P5 p5)
        {
            bool p1;
            (p1, p2, p3, p4, p5) = tuple;
            return p1;
        }

        public static void AllOut<P1, P2>(this Tuple<P1, P2> tuple, out P1 p1, out P2 p2)
        {
            (p1, p2) = tuple;
        }

        public static void AllOut<P1, P2, P3>(this Tuple<P1, P2, P3> tuple, out P1 p1, out P2 p2, out P3 p3)
        {
            (p1, p2, p3) = tuple;
        }

        public static void AllOut<P1, P2, P3, P4>(this Tuple<P1, P2, P3, P4> tuple, out P1 p1, out P2 p2, out P3 p3, out P4 p4)
        {
            (p1, p2, p3, p4) = tuple;
        }

        public static void AllOut<P1, P2, P3, P4, P5>(this Tuple<P1, P2, P3, P4, P5> tuple, out P1 p1, out P2 p2, out P3 p3, out P4 p4, out P5 p5)
        {
            (p1, p2, p3, p4, p5) = tuple;
        }
    }
}