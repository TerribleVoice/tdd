﻿using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace CloudTag
{
    public class CircularCloudLayouterTests
    {
        private CircularCloudLayouter layouter;
        private readonly Point layouterCenter = new Point(0, 0);
        private string failTestsImgsPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            failTestsImgsPath = Path.Combine(Environment.CurrentDirectory, "Failures Tests Layouts");
            if (!Directory.Exists(failTestsImgsPath))
                Directory.CreateDirectory(failTestsImgsPath);

            foreach (var filePath in Directory.GetFiles(failTestsImgsPath))
                File.Delete(filePath);
        }
        
        [SetUp]
        public void SetUp()
        {
            layouter = new CircularCloudLayouter(layouterCenter);
        }

        [TearDown]
        public void TearDown()
        {
            if (TestContext.CurrentContext.Result.Outcome != ResultState.Failure)
                return;

            var name = TestContext.CurrentContext.Test.MethodName;
            if (TestContext.CurrentContext.Test.MethodName != TestContext.CurrentContext.Test.Name)
                name += $" {TestContext.CurrentContext.Test.Name}";

            var fullPath = Path.Combine(failTestsImgsPath, $"{name}.png");
            CloudPainter.DrawAndSaveToFile(Color.Red, layouter.GetRectangles(), fullPath);
            
            TestContext.Out.WriteLine($"Tag cloud visualization saved to file {fullPath}");
        }

        [Test]
        public void Constructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new CircularCloudLayouter(new Point(0, 0)));
        }

        [Test]
        public void PutNextRectangle_ReturnsFirstRectangle_WithCenterInLayouterCenter()
        {
            var size = new Size(10, 10);
            var actualRectangle = layouter.PutNextRectangle(size);
            var expectedRectangle = new Rectangle {Size = size};

            expectedRectangle.SetCenter(layouterCenter);

            actualRectangle.Should().Be(expectedRectangle);
        }

        [TestCase(2)]
        [TestCase(10)]
        [TestCase(80)]
        [TestCase(200)]
        public void PutNextRectangle_ShouldNotIntersect_RectSameSize(int rectCount)
        {
            var size = new Size(10, 10);
            var rectangles = Enumerable.Range(0, rectCount)
                .Select(i => layouter.PutNextRectangle(size))
                .ToArray();

            for (var i = 0; i < rectCount; i++)
            for (var j = i + 1; j < rectCount; j++)
                rectangles[i].IntersectsWith(rectangles[j]).Should()
                    .BeFalse($"{rectangles[i]} should not intersect {rectangles[j]}");
        }

        [Test]
        public void PutNextRectangle_ReturnEmptyRect_SizeIsZero()
        {
            var actualRect = layouter.PutNextRectangle(new Size(0, 0));
            actualRect.Should().Be(Rectangle.Empty);
        }

        [TestCase(-10, 10)]
        [TestCase(10, -10)]
        [TestCase(-10, -10)]
        public void PutNextRectangle_ShouldThrow_SizeWithNegativeSides(int width, int height)
        {
            Assert.Throws<ArgumentException>(() => layouter.PutNextRectangle(new Size(width, height)));
        }

        [TestCase(1)]
        [TestCase(20)]
        public void GetRectangles_ReturnEmptyCollection_AddedRectsWithZeroSize(int rectCount)
        {
            var size = new Size(0, 0);
            for (var i = 0; i < rectCount; i++)
                layouter.PutNextRectangle(size);

            layouter.GetRectangles().Should().BeEmpty();
        }

        [Test]
        public void PutNextRectangle_ReturnsEmptyRect_OneOfSidesIs0()
        {
            var actualRect1 = layouter.PutNextRectangle(new Size(0, 10));
            var actualRect2 = layouter.PutNextRectangle(new Size(10, 0));

            actualRect1.Should().Be(Rectangle.Empty);
            actualRect2.Should().Be(Rectangle.Empty);
        }

        [Timeout(1000)]
        [Test]
        public void PutNextRectangle_PerformanceTest_5000RectSameSize()
        {
            var size = new Size(10, 10);
            for (var i = 0; i < 5000; i++)
                layouter.PutNextRectangle(size);
        }

        [TestCase(10)]
        [TestCase(20)]
        [TestCase(50)]
        public void PutNextRectangle_LayoutShouldBeTight_RectsDifferentSize(int rectCount)
        {
            for (var i = 0; i < rectCount; i++)
                layouter.PutNextRectangle(new Size((i * 15 + 25) % 90 + 30, (i * 10 + 20) % 60 + 20));

            var maxDistanceToCenter = layouter.GetRectangles().Max(rectangle =>
                Math.Sqrt(Math.Pow(rectangle.Location.X, 2) +
                          Math.Pow(rectangle.Location.Y, 2)));

            var commonArea = layouter.GetRectangles().Sum(rectangle => rectangle.Height * rectangle.Width);
            var borderCircleArea = maxDistanceToCenter * maxDistanceToCenter * Math.PI;

            borderCircleArea.Should().BeLessThan(3 * commonArea);
        }

        [TestCase(10)]
        [TestCase(20)]
        [TestCase(50)]
        public void PutNextRectangle_LayoutShouldBeRoundShape_RectsDifferentSize(int rectCount)
        {
            for (var i = 0; i < rectCount; i++)
                layouter.PutNextRectangle(new Size((i * 15 + 25) % 90 + 30, (i * 5 + 20) % 60 + 20));

            var commonArea = layouter.GetRectangles().Sum(rectangle => rectangle.Height * rectangle.Width);
            var suggestedCircleRadius = Math.Sqrt(commonArea / Math.PI);

            foreach (var rectangle in layouter.GetRectangles())
            {
                var distanceToCenter = Math.Sqrt(Math.Pow(rectangle.Location.X, 2) +
                                                 Math.Pow(rectangle.Location.Y, 2));

                distanceToCenter.Should().BeLessThan(1 * suggestedCircleRadius / 2);
            }
        }
    }
}