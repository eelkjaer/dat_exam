import React, { Component } from 'react';
import moment from 'moment'

export class Home extends Component {
    static displayName = Home.name;

    constructor(props) {
        super(props);
        this.state = { forecasts: [], loading: true };
    }

    componentDidMount() {
        this.populateData();
    }

    static renderCrtTable(forecasts) {
        const dateFormat = 'MMM YYYY';
        
        //sort forecasts.employees by fullName
        forecasts.employees.sort((a, b) => (a.fullName > b.fullName) ? 1 : -1);

        function FindOnboardingsPerMonth(date, employee) {
            //count employee.onboardings where kickoffdate is same or before date or golivedate is same or after date
            var onboardings = employee.onboardings.filter(
                onboarding => moment(onboarding.formData.kickOffDate)
                    .isSameOrBefore(date) 
                    && moment(onboarding.formData.goLiveDate)
                        .isSameOrAfter(date));
            return onboardings;
        }
        
        function FindCapacity(date, employee){
            
            var newDate = moment(date).format("MMMM")
            
            var capacity = employee.capacity.find(
                capacity => capacity.month === newDate);
            
            if(capacity.capacity == null){
                capacity.capacity = 0;
            }
            
            console.log(capacity)
            
            return capacity;
        }
        
        return (
            <table className='table table-striped' aria-labelledby="tabelLabel">
                <thead>
                <tr>
                    <th></th>
                    {forecasts.dates.map(date => 
                    <th>{moment(date).format(dateFormat)}</th>
                    )}
                </tr>
                </thead>
                <tbody>
                {forecasts.employees.map(employee =>
                    
                    <tr key={employee.id}>
                        <td>{employee.fullName}</td>

                        {forecasts.dates.map(date =>
                            <td>{FindOnboardingsPerMonth(date, employee).length} / {FindCapacity(date, employee).capacity}</td>,
                        )}
                    </tr>
                )}
                </tbody>
            </table>
        );
    }

    render() {
        const dateFormat = 'MMM YYYY';    
        let startDate = this.state.loading ? "" : moment(this.state.forecasts.dates[0]).format(dateFormat);
        let endDate = this.state.loading ? "" : moment(this.state.forecasts.dates.slice(-1)[0]).format(dateFormat);
        let contents = <p><em>Loading...</em></p>;
        
        if (!this.state.loading) {
            //Show table
            contents = Home.renderCrtTable(this.state.forecasts);
        }

        return (
            <div>
                <h3 id="tabelLabel" >{startDate} - {endDate}</h3>
                {contents}
            </div>
        );
    }

    async populateData() {
        const response = await fetch('forecast');
        const data = await response.json();
        this.setState({ forecasts: data, loading: false });
        console.log(data);
    }
}
