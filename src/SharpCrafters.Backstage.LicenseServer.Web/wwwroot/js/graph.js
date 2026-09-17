// Renders the licence usage history. The data is produced by GraphModel and embedded in the page as
// JSON; nothing is fetched at run time, so the page works on an isolated network.
(function () {
    "use strict";

    var dataElement = document.getElementById("usage-chart-data");
    var canvas = document.getElementById("usage-chart");

    if (!dataElement || !canvas || typeof Chart === "undefined") {
        return;
    }

    var chart = JSON.parse(dataElement.textContent);

    // Take the axis and gridline colours from the design tokens, so the chart stays part of the
    // page rather than a white box dropped onto it.
    var styles = getComputedStyle(document.documentElement);
    Chart.defaults.color = styles.getPropertyValue("--text-muted").trim() || "#a0a0a0";
    Chart.defaults.borderColor = styles.getPropertyValue("--doc-table-line").trim() || "#2c1a4f";
    Chart.defaults.font.family = styles.getPropertyValue("--font-body").trim() || "sans-serif";

    var datasets = [{
        label: "Seats",
        data: chart.seats,
        borderColor: "#973bfc",
        backgroundColor: "rgba(151, 59, 252, 0.16)",
        fill: true,
        tension: 0.1,
        pointRadius: 0,
        pointHitRadius: 8
    }];

    // The capacity and grace lines are absent for an unlimited licence.
    if (chart.maximum !== null && chart.maximum !== undefined) {
        datasets.push({
            label: "Authorized",
            data: chart.labels.map(function () { return chart.maximum; }),
            borderColor: "#38d5e4",
            borderDash: [6, 4],
            fill: false,
            pointRadius: 0
        });

        datasets.push({
            label: "Grace",
            data: chart.labels.map(function () { return chart.grace; }),
            borderColor: "#ff5e45",
            borderDash: [2, 3],
            fill: false,
            pointRadius: 0
        });
    }

    new Chart(canvas, {
        type: "line",
        data: { labels: chart.labels, datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: "index", intersect: false },
            scales: {
                y: {
                    beginAtZero: true,
                    max: chart.axisMaximum,
                    title: { display: true, text: "Seats" },
                    ticks: { precision: 0 }
                },
                x: {
                    ticks: {
                        autoSkip: false,
                        maxRotation: 45,
                        minRotation: 45,
                        // The original chart labelled Mondays only, which keeps a year-long window
                        // readable.
                        callback: function (value, index) {
                            var label = chart.labels[index];
                            return new Date(label + "T00:00:00Z").getUTCDay() === 1 ? label : "";
                        }
                    }
                }
            },
            plugins: {
                legend: {
                    position: "bottom",

                    // Each series is a line, so the legend samples it as a line rather than as the
                    // filled rectangle Chart.js draws by default. The dash pattern and the width
                    // have to be carried over from the dataset, because the generated legend item
                    // does not take them, and without them the three samples differ only by colour
                    // while the lines on the chart are solid, dashed and dotted.
                    labels: {
                        usePointStyle: true,
                        pointStyle: "line",
                        boxWidth: 32,
                        generateLabels: function (instance) {
                            var items = Chart.defaults.plugins.legend.labels.generateLabels(instance);

                            items.forEach(function (item) {
                                var dataset = instance.data.datasets[item.datasetIndex];
                                item.lineDash = dataset.borderDash || [];
                                item.lineWidth = dataset.borderWidth || 2;
                            });

                            return items;
                        }
                    }
                },
                tooltip: {
                    callbacks: {
                        title: function (items) { return items[0].label; }
                    }
                }
            }
        }
    });
})();
