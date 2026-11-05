(function () {
    "use strict";
    // Renders the server-provided revenue points; the table below the chart stays the accessible source.
    const data = document.getElementById("billing-revenue-data");
    const canvas = document.getElementById("billing-revenue-chart");
    if (!data || !canvas || typeof Chart === "undefined") return;
    const points = JSON.parse(data.textContent || "{}");
    new Chart(canvas.getContext("2d"), {
        type: "bar",
        data: {
            labels: points.labels || [],
            datasets: [
                { label: "Doanh thu (đ)", data: points.revenue || [], yAxisID: "y" },
                { label: "Đơn thanh toán", data: points.paid || [], type: "line", yAxisID: "y1" }
            ]
        },
        options: {
            responsive: true,
            scales: {
                y: { beginAtZero: true, position: "left" },
                y1: { beginAtZero: true, position: "right", grid: { drawOnChartArea: false }, ticks: { precision: 0 } }
            }
        }
    });
})();
